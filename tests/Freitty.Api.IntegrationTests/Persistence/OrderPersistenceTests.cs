using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Persistence;

public sealed class OrderPersistenceTests(PostgresFixture postgres) : IAsyncDisposable
{
    private readonly TestDatabase database = new(postgres.ConnectionString);

    [Fact]
    public async Task OrderBuiltThroughAggregateIsReadBackIntact()
    {
        TestData data = TestData.Create();
        Order order = BuildOrderWithEverything(data);
        await database.SaveAsync([.. data.Entities(), order]);

        Order loaded = await database.InContextAsync(db => LoadWithReferencesAsync(db, order.Id));

        Assert.Equal(2, loaded.Shipments.Count);
        Assert.Equal(3, loaded.Shipments.SelectMany(shipment => shipment.Alerts).Count());
        Assert.Equivalent(order, loaded, strict: true);
    }

    [Fact]
    public async Task ServiceAddedToLoadedDraftIsInsertedAlone()
    {
        TestData data = TestData.Create();
        Order draft = data.Draft();
        TestData.Succeed(draft.AddService(data.Service.Id, TestData.Now));
        await database.SaveAsync([.. data.Entities(), draft]);

        Type[] addedTypes = await database.InContextAsync(async db =>
        {
            Order loaded = await db.LoadOrderAsync(draft.Id);
            TestData.Succeed(loaded.AddService(data.OtherService.Id, TestData.Now));
            return await SaveReportingAddedTypesAsync(db);
        });

        Order reloaded = await database.InContextAsync(db => db.LoadOrderAsync(draft.Id));
        Assert.Equal([typeof(OrderService)], addedTypes);
        Assert.Equivalent(
            new[] { data.Service.Id, data.OtherService.Id },
            reloaded.Services.Select(service => service.ServiceId));
    }

    [Fact]
    public async Task OperationAndSupplyLineAddedToLoadedSubmittedOrderAreInsertedAlone()
    {
        TestData data = TestData.Create();
        Order order = data.Ready();
        await database.SaveAsync([.. data.Entities(), order]);

        Type[] addedTypes = await database.InContextAsync(async db =>
        {
            Order loaded = await db.LoadOrderAsync(order.Id);
            TestData.Succeed(loaded.AddSupply(data.SupplyItem, 2, TestData.Now));
            NewOperation unloading = TestData.DeclaredUnloading(photoCount: 1);
            _ = loaded.RecordOperation(loaded.Shipments.Single().Id, unloading, TestData.Now).Value;
            return await SaveReportingAddedTypesAsync(db);
        });

        Order reloaded = await database.InContextAsync(db => db.LoadOrderAsync(order.Id));
        Assert.Equivalent(new[] { typeof(Operation), typeof(SupplyLine) }, addedTypes, strict: true);
        Assert.Single(reloaded.Shipments.Single().Operations);
        Assert.Single(reloaded.SupplyLines);
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    // Instants carry a hub-local offset on purpose: the aggregate must store them in UTC.
    private static Order BuildOrderWithEverything(TestData data)
    {
        var start = new DateTimeOffset(2026, 3, 2, 8, 0, 0, TimeSpan.FromHours(-5));
        Order order = Order.StartDraft(
            TestData.Unique("DRAFT-"),
            OrderType.Consolidation,
            crossDockKind: null,
            data.User,
            data.Hub.Id,
            start);
        Shipment first = order.AddShipment("REF-1001", start).Value;
        Shipment second = order.AddShipment("REF-1002", start).Value;
        TestData.Succeed(order.DeclareCargo(first.Id, UnitType.Standard, 10, start));
        TestData.Succeed(order.DeclareCargo(first.Id, UnitType.ExtraLarge, 2, start));
        TestData.Succeed(order.DeclareCargo(second.Id, UnitType.Standard, 5, start));
        TestData.Succeed(order.Schedule(start.AddDays(1), start));
        TestData.Succeed(order.SetDestination("Montreal", "QC", start));
        TestData.Succeed(order.ArrangeOwnDriver("Jean Tremblay", start));
        TestData.Succeed(order.AddService(data.Service.Id, start));
        TestData.Succeed(order.AddSupply(data.SupplyItem, 3, start));
        TestData.Succeed(order.Submit(TestData.Unique("FR"), start.AddMinutes(5)));

        DateTimeOffset hubWork = start.AddHours(2);
        TestData.Succeed(order.TransferTo(data.OtherHub.Id, hubWork));
        TestData.Succeed(order.RecordArrival(first.Id, TrailerType.Reefer53, "tr-77", "tl-88", hubWork));
        TestData.Succeed(order.AssignDock(first.Id, "D4", "B2", "Floor Lead", hubWork));
        TestData.Succeed(order.SetWarehouseNote(first.Id, "Fragile freight", hubWork));
        var excessWithoutPhoto = new NewOperation(
            OperationType.Unloading,
            UnitType.Standard,
            Quantity: 12,
            TrailerNumber: "tl-88",
            IsBillable: true,
            PhotoCount: 0,
            CommentCount: 2);
        var restack = new NewOperation(
            OperationType.Restack,
            UnitType.Standard,
            Quantity: 4,
            TrailerNumber: null,
            IsBillable: false,
            PhotoCount: 1,
            CommentCount: 0);
        _ = order.RecordOperation(first.Id, excessWithoutPhoto, hubWork.AddMinutes(10)).Value;
        _ = order.RecordOperation(first.Id, restack, hubWork.AddMinutes(20)).Value;
        Alert missingPhoto = first.Alerts.Single(alert => alert.Type == AlertType.MissingPhoto);
        TestData.Succeed(order.ResolveAlert(missingPhoto.Id, hubWork.AddMinutes(30)));
        TestData.Succeed(order.FinishReceiving(second.Id, hubWork.AddMinutes(40)));
        return order;
    }

    // Saving filled the navigations of the original from the tracked reference data, so the copy read back loads
    // them too; this also checks that each foreign key maps to its navigation.
    private static Task<Order> LoadWithReferencesAsync(AppDbContext db, Guid orderId)
    {
        return db.Orders
            .Include(order => order.Company)
            .Include(order => order.CreatedBy)
            .Include(order => order.Hub)
            .Include(order => order.TransferHub)
            .Include(order => order.Carrier)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.CargoLines)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Operations)
            .Include(order => order.Shipments).ThenInclude(shipment => shipment.Alerts)
            .Include(order => order.SupplyLines).ThenInclude(line => line.SupplyItem)
            .Include(order => order.Services).ThenInclude(service => service.Service)
            .AsSplitQuery()
            .SingleAsync(order => order.Id == orderId, TestContext.Current.CancellationToken);
    }

    private static async Task<Type[]> SaveReportingAddedTypesAsync(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        Type[] addedTypes = db.ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity.GetType())
            .ToArray();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return addedTypes;
    }
}
