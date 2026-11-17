namespace Freitty.Domain.Orders;

public sealed class Alert
{
    internal Alert(Guid shipmentId, AlertType type, DateTimeOffset raisedAt)
    {
        Id = Guid.CreateVersion7();
        ShipmentId = shipmentId;
        Type = type;
        RaisedAt = raisedAt;
    }

    private Alert()
    {
    }

    public Guid Id { get; private set; }

    public Guid ShipmentId { get; private set; }

    public AlertType Type { get; private set; }

    public DateTimeOffset RaisedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    internal bool IsOpen => ResolvedAt is null;

    internal void Resolve(DateTimeOffset now)
    {
        ResolvedAt = now;
    }
}
