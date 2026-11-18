using System.Net;
using System.Text.Json;
using Freitty.Application.Orders;
using Freitty.Application.Orders.AttachPhotos;
using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// Every refusal leaves the order and the storage as they were: the version does not move and no upload is stored.
[Collection(PhotoStorageCollection.Name)]
public sealed class AttachPhotosRefusalTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly CabinetApi api = new(postgres.ConnectionString);
    private string[] storedBeforeTheRequest = [];

    [Fact]
    public async Task UploadWithoutAFileIsABadRequest()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response = await api.UploadAsync(order.Number, unloading.Id, order.Version);

        JsonElement problem = await ProblemResponse.AssertValidationProblemAsync(response, "files");
        Assert.Equal("Choose at least one photo.", problem.GetProperty("detail").GetString());
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task SixFilesAreABadRequest()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response = await api.UploadAsync(
            order.Number,
            unloading.Id,
            order.Version,
            PhotoUpload.Pngs(AttachPhotosCommandValidator.MaxFileCount + 1));

        JsonElement problem = await ProblemResponse.AssertValidationProblemAsync(response, "files");
        Assert.Equal(
            "At most 5 photos can be uploaded at once; 6 were sent.",
            problem.GetProperty("detail").GetString());
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task FileOverTheSizeLimitIsABadRequestNamingIt()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response = await api.UploadAsync(
            order.Number,
            unloading.Id,
            order.Version,
            PhotoUpload.Png("first.png"),
            PhotoUpload.Jpeg("large.jpg", (int)PhotoFileValidator.MaxSizeBytes + 1));

        JsonElement problem = await ProblemResponse.AssertValidationProblemAsync(response, "files[1]");
        Assert.Equal("large.jpg is larger than 5 MB.", problem.GetProperty("detail").GetString());
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task TextFileNamedJpegIsABadRequestNamingIt()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response = await api.UploadAsync(
            order.Number,
            unloading.Id,
            order.Version,
            PhotoUpload.TextNamedJpeg("notes.jpg"));

        JsonElement problem = await ProblemResponse.AssertValidationProblemAsync(response, "files[0]");
        Assert.Equal("notes.jpg is not a JPEG, PNG or WebP image.", problem.GetProperty("detail").GetString());
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task EmptyFileIsABadRequest()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response = await api.UploadAsync(
            order.Number,
            unloading.Id,
            order.Version,
            new PhotoUpload("empty.png", [], "image/png"));

        await ProblemResponse.AssertValidationProblemAsync(response, "files[0]");
        await AssertNothingChangedAsync(order);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task VersionMissingOrNotPositiveIsABadRequest(int? version)
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, unloading.Id, version, PhotoUpload.Png());

        await ProblemResponse.AssertValidationProblemAsync(response, "version");
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task VersionThatIsNotANumberIsABadRequest()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();
        using var form = new MultipartFormDataContent { { new StringContent("seven"), PhotoUpload.VersionFormField } };
        PhotoUpload.Png().AddTo(form);

        using HttpResponseMessage response =
            await api.SendFormAsync(ApiUrls.OperationPhotos(order.Number, unloading.Id), form);

        await ProblemResponse.AssertValidationProblemAsync(response, "version");
    }

    [Fact]
    public async Task OperationIdThatIsNotAUuidIsABadRequest()
    {
        (Order order, _) = await SaveUnloadedAsync();
        using MultipartFormDataContent form = PhotoUpload.Form(order.Version, PhotoUpload.Png());
        using HttpResponseMessage response =
            await api.SendFormAsync(ApiUrls.OperationPhotos(order.Number, "not-a-uuid"), form);

        await ProblemResponse.AssertValidationProblemAsync(response, "operationId");
        await AssertNothingChangedAsync(order);
    }

    // At the bound of the demo, a refusal the aggregate makes still comes first: it is final.
    [Fact]
    public async Task UnknownOperationAtTheBoundOfTheDemoIsNotFound()
    {
        await using var bounded = new CabinetApi(await postgres.CreateDatabaseAsync());
        Order order = bounded.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0);
        AttachUploadRows(order, AttachPhotosCommandHandler.MaxUploadedPhotoCount);
        await bounded.SaveAsync(order);

        using HttpResponseMessage response =
            await bounded.UploadAsync(order.Number, Guid.CreateVersion7(), order.Version, PhotoUpload.Png());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task BodyThatIsNotAFormIsUnsupported()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response = await api.SendRawPostAsync(
            ApiUrls.OperationPhotos(order.Number, unloading.Id),
            $$"""{ "version": {{order.Version}} }""");

        await ProblemResponse.AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType);
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task StaleVersionIsAConflictAndStoresNothing()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, unloading.Id, order.Version - 1, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            OrderRequestErrors.VersionConflictCode,
            $"Order {order.Number} was changed since you loaded it (version {order.Version}, expected " +
            $"{order.Version - 1}). Reload and try again.");
        await AssertNothingChangedAsync(order);
    }

    // The competing write commits inside the request's save, after the bytes were stored: they are deleted again.
    [Fact]
    public async Task UploadThatLosesTheRaceToAnotherWriteLeavesNoUpload()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync();
        api.CompetingSave.Arm(() => api.InContextAsync(async db =>
        {
            Order competing = await db.LoadOrderAsync(order.Id);
            OrderBuilder.Receive(competing, competing.Shipments.Single(), CabinetApi.Now);
            await db.SaveChangesAsync(CancellationToken);
        }));

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, unloading.Id, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            OrderRequestErrors.VersionConflictCode,
            $"Order {order.Number} was changed while this request was being processed. Reload and try again.");
        Assert.Equal(storedBeforeTheRequest, await api.StoredUploadsAsync());
        Assert.Equal(0, (await api.DetailAsync(order.Number)).Totals.PhotoCount);
    }

    [Fact]
    public async Task UploadToACompletedOrderIsAConflict()
    {
        Order order = api.Orders.Completed();
        await SaveAsync(order);
        Operation unloading = order.Shipments.Single().Operations.First();

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, unloading.Id, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            OrderErrors.NotActiveCode,
            $"Order {order.Number} is Completed; the hub works only on a Ready or InProgress order.");
        await AssertNothingChangedAsync(order);
    }

    [Fact]
    public async Task EleventhPhotoOfAnOperationIsAConflict()
    {
        (Order order, Operation unloading) = await SaveUnloadedAsync(photos: Operation.MaxPhotoCount);

        using HttpResponseMessage response =
            await api.UploadAsync(order.Number, unloading.Id, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            response,
            HttpStatusCode.Conflict,
            OrderErrors.PhotoLimitCode,
            $"The Unloading of {order.Number}-1 has 10 photos; 1 more would exceed the limit of 10.");
        await AssertNothingChangedAsync(order);
    }

    // A database of its own: the bound counts the uploads of every company.
    [Fact]
    public async Task UploadBeyondTheBoundOfTheDemoIsAConflict()
    {
        await using var bounded = new CabinetApi(await postgres.CreateDatabaseAsync());
        Order order = bounded.Orders.Ready();
        Shipment shipment = order.Shipments.Single();
        OrderBuilder.Unload(order, shipment, OrderBuilder.DeclaredStandard, photos: 0);
        Guid operationId = shipment.Operations.Single().Id;
        AttachUploadRows(order, AttachPhotosCommandHandler.MaxUploadedPhotoCount - 1);
        await bounded.SaveAsync(order);

        using HttpResponseMessage beyond =
            await bounded.UploadAsync(order.Number, operationId, order.Version, PhotoUpload.Pngs(2));
        using HttpResponseMessage last =
            await bounded.UploadAsync(order.Number, operationId, order.Version, PhotoUpload.Png());

        await ProblemResponse.AssertDomainProblemAsync(
            beyond,
            HttpStatusCode.Conflict,
            PhotoRequestErrors.UploadLimitCode,
            "The demo already holds the most uploaded photos it keeps, 200. They are deleted when the sample data " +
            "is next rebuilt; try again after that.");
        Assert.Equal(HttpStatusCode.Created, last.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    // Rows only, under uploads/ as an upload's are, spread over Restacks of the shipment's stock.
    private static void AttachUploadRows(Order order, int rowCount)
    {
        Shipment shipment = order.Shipments.Single();
        for (int added = 0; added < rowCount; added += Operation.MaxPhotoCount)
        {
            OrderBuilder.Restack(order, shipment, 1);
            Guid restackId = shipment.Operations.Last().Id;
            IReadOnlyList<NewPhoto> uploads = Enumerable
                .Range(0, Math.Min(Operation.MaxPhotoCount, rowCount - added))
                .Select(_ => new NewPhoto($"uploads/{Guid.CreateVersion7()}", "image/png", 1024))
                .ToList();
            order.AttachPhotos(restackId, uploads, OrderBuilder.BuiltAt).MustSucceed();
        }
    }

    private async Task<(Order Order, Operation Unloading)> SaveUnloadedAsync(int photos = 0)
    {
        Order order = api.Orders.Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: photos);
        await SaveAsync(order);
        return (order, order.Shipments.Single().Operations.Single());
    }

    private async Task SaveAsync(Order order)
    {
        await api.SaveAsync(order);
        storedBeforeTheRequest = await api.StoredUploadsAsync();
    }

    private async Task AssertNothingChangedAsync(Order order)
    {
        Assert.Equal(order.Version, (await api.DetailAsync(order.Number)).Version);
        Assert.Equal(storedBeforeTheRequest, await api.StoredUploadsAsync());
    }
}
