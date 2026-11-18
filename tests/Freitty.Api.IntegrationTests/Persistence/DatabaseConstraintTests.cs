using Freitty.Domain.Orders;
using Freitty.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Freitty.Api.IntegrationTests.Persistence;

// The aggregate would refuse each of these writes first, so the offending row is written with raw SQL: the point
// is that a row written past the domain still cannot break the rule.
public sealed class DatabaseConstraintTests(PostgresFixture postgres) : IAsyncDisposable
{
    private const int StatusOutsideEnum = 99;

    private readonly TestDatabase database = new(postgres.ConnectionString);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SecondOpenAlertOfOneTypeOnShipmentIsRefused()
    {
        (TestData data, Order order) = WithOpenMissingPhotoAlert();
        await database.SaveAsync([.. data.Entities(), order]);

        await AssertRefusedAsync(
            PostgresErrorCodes.UniqueViolation,
            "ix_alerts_shipment_id_type",
            db => InsertMissingPhotoAlertAsync(db, order.Shipments.Single().Id));
    }

    [Fact]
    public async Task SecondAlertOfOneTypeIsAcceptedOnceFirstIsResolved()
    {
        (TestData data, Order order) = WithOpenMissingPhotoAlert();
        Shipment shipment = order.Shipments.Single();
        order.ResolveAlert(shipment.Alerts.Single().Id, OrderBuilder.BuiltAt).MustSucceed();
        await database.SaveAsync([.. data.Entities(), order]);

        await database.InContextAsync(db => InsertMissingPhotoAlertAsync(db, shipment.Id));

        int alertCount = await database.InContextAsync(
            db => db.Set<Alert>().CountAsync(alert => alert.ShipmentId == shipment.Id, CancellationToken));
        Assert.Equal(2, alertCount);
    }

    [Fact]
    public async Task DuplicateOrderNumberWithinCompanyIsRefused()
    {
        TestData data = TestData.Create();
        var orders = new OrderBuilder(data);
        Order first = orders.Draft();
        Order second = orders.Draft();
        await database.SaveAsync([.. data.Entities(), first, second]);

        await AssertRefusedAsync(
            PostgresErrorCodes.UniqueViolation,
            "ix_orders_company_id_number",
            db => db.Database.ExecuteSqlAsync(
                $"UPDATE orders SET number = {first.Number} WHERE id = {second.Id}",
                CancellationToken));
    }

    [Fact]
    public async Task SameOfficialNumberInTwoCompaniesIsRefused()
    {
        TestData firstCompany = TestData.Create();
        TestData secondCompany = TestData.Create();
        Order first = new OrderBuilder(firstCompany).Ready();
        Order second = new OrderBuilder(secondCompany).Ready();
        await database.SaveAsync([.. firstCompany.Entities(), .. secondCompany.Entities(), first, second]);

        await AssertRefusedAsync(
            PostgresErrorCodes.UniqueViolation,
            "ix_orders_number",
            db => db.Database.ExecuteSqlAsync(
                $"UPDATE orders SET number = {first.Number} WHERE id = {second.Id}",
                CancellationToken));
    }

    [Fact]
    public async Task TwoCompaniesMayHoldTheSameDraftNumber()
    {
        TestData firstCompany = TestData.Create();
        TestData secondCompany = TestData.Create();
        string draftNumber = TestData.Unique("DRAFT-");

        await database.SaveAsync(
            [.. firstCompany.Entities(), .. secondCompany.Entities(),
                new OrderBuilder(firstCompany).Draft(draftNumber: draftNumber),
                new OrderBuilder(secondCompany).Draft(draftNumber: draftNumber)]);

        int orderCount = await database.InContextAsync(
            db => db.Orders.CountAsync(order => order.Number == draftNumber, CancellationToken));
        Assert.Equal(2, orderCount);
    }

    [Fact]
    public async Task CargoLineWithQuantityZeroIsRefused()
    {
        TestData data = TestData.Create();
        Order draft = new OrderBuilder(data).Draft();
        Shipment shipment = draft.AddShipment(TestData.Unique("REF-"), OrderBuilder.BuiltAt).MustSucceed();
        await database.SaveAsync([.. data.Entities(), draft]);

        await AssertRefusedAsync(
            PostgresErrorCodes.CheckViolation,
            "ck_cargo_lines_quantity",
            db => db.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO cargo_lines (id, shipment_id, unit_type, quantity)
                VALUES ({Guid.CreateVersion7()}, {shipment.Id}, {(int)UnitType.Standard}, 0)
                """,
                CancellationToken));
    }

    [Fact]
    public async Task CarrierOnOrderWithoutTransportKindIsRefused()
    {
        TestData data = TestData.Create();
        Order draft = new OrderBuilder(data).Draft();
        await database.SaveAsync([.. data.Entities(), draft]);

        await AssertRefusedAsync(
            PostgresErrorCodes.CheckViolation,
            "ck_orders_carrier_matches_transport",
            db => db.Database.ExecuteSqlAsync(
                $"UPDATE orders SET carrier_id = {data.Carrier.Id} WHERE id = {draft.Id}",
                CancellationToken));
    }

    [Fact]
    public async Task CompletedOrderWithoutCompletedAtIsRefused()
    {
        TestData data = TestData.Create();
        Order order = InProgress(data);
        await database.SaveAsync([.. data.Entities(), order]);

        await AssertRefusedAsync(
            PostgresErrorCodes.CheckViolation,
            "ck_orders_completed_at_matches_status",
            db => db.Database.ExecuteSqlAsync(
                $"UPDATE orders SET status = {(int)OrderStatus.Completed} WHERE id = {order.Id}",
                CancellationToken));
    }

    // A Ready order: the only status whose milestones a status outside the enum also satisfies, so the enum
    // check is the one that answers.
    [Fact]
    public async Task StatusOutsideEnumIsRefused()
    {
        TestData data = TestData.Create();
        Order order = new OrderBuilder(data).Ready();
        await database.SaveAsync([.. data.Entities(), order]);

        await AssertRefusedAsync(
            PostgresErrorCodes.CheckViolation,
            "ck_orders_status",
            db => db.Database.ExecuteSqlAsync(
                $"UPDATE orders SET status = {StatusOutsideEnum} WHERE id = {order.Id}",
                CancellationToken));
    }

    [Fact]
    public async Task DeletingDraftRemovesItsShipmentsAndCargoLines()
    {
        TestData data = TestData.Create();
        var orders = new OrderBuilder(data);
        Order draft = orders.Draft();
        orders.AddShipment(draft, OrderBuilder.DeclaredStandard);
        Guid shipmentId = draft.Shipments.Single().Id;
        await database.SaveAsync([.. data.Entities(), draft]);

        await database.InContextAsync(
            db => db.Orders.Where(order => order.Id == draft.Id).ExecuteDeleteAsync(CancellationToken));

        (int Shipments, int CargoLines) remaining = await database.InContextAsync(async db =>
            (await db.Set<Shipment>().CountAsync(shipment => shipment.OrderId == draft.Id, CancellationToken),
                await db.Set<CargoLine>().CountAsync(line => line.ShipmentId == shipmentId, CancellationToken)));
        Assert.Equal((0, 0), remaining);
    }

    [Fact]
    public async Task DeletingOrderWithOperationIsRefused()
    {
        TestData data = TestData.Create();
        Order order = new OrderBuilder(data).Ready();
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 1);
        await database.SaveAsync([.. data.Entities(), order]);

        await AssertRefusedAsync(
            PostgresErrorCodes.RestrictViolation,
            "fk_operations_shipments_shipment_id",
            db => db.Orders.Where(candidate => candidate.Id == order.Id).ExecuteDeleteAsync(CancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        await database.DisposeAsync();
    }

    // Unloaded as declared, so the photo is the only thing missing.
    private static (TestData Data, Order Order) WithOpenMissingPhotoAlert()
    {
        TestData data = TestData.Create();
        Order order = InProgress(data);
        OrderBuilder.Unload(order, order.Shipments.Single(), OrderBuilder.DeclaredStandard, photos: 0);
        return (data, order);
    }

    private static Order InProgress(TestData data)
    {
        Order order = new OrderBuilder(data).Ready();
        OrderBuilder.Arrive(order, order.Shipments.Single());
        return order;
    }

    private static Task<int> InsertMissingPhotoAlertAsync(AppDbContext db, Guid shipmentId)
    {
        return db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO alerts (id, shipment_id, type, raised_at)
            VALUES ({Guid.CreateVersion7()}, {shipmentId}, {(int)AlertType.MissingPhoto}, {OrderBuilder.BuiltAt})
            """,
            CancellationToken);
    }

    private async Task AssertRefusedAsync(string sqlState, string constraintName, Func<AppDbContext, Task> write)
    {
        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(() => database.InContextAsync(write));
        Assert.Equal(sqlState, exception.SqlState);
        Assert.Equal(constraintName, exception.ConstraintName);
    }
}
