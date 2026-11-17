using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence.Seed;

// What an order's script says about its invoice; Invoices issues it once every invoice of the seed is known.
internal sealed record DueInvoice(Order Order, DateTimeOffset IssuedAt, DateTimeOffset? PaidAt);
