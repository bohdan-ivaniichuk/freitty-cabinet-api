namespace Freitty.Application.Orders.GetOrderDetail;

// PaidAt is null while the invoice is unpaid.
public sealed record InvoiceDto(string Number, decimal Amount, DateTimeOffset IssuedAt, DateTimeOffset? PaidAt);
