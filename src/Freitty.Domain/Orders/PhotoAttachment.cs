namespace Freitty.Domain.Orders;

// What attaching photos did: the photos added to the operation and the alerts of its shipment this closed.
public sealed record PhotoAttachment(
    Operation Operation,
    IReadOnlyList<Photo> Photos,
    IReadOnlyList<AlertType> ResolvedAlertTypes);
