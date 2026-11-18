using Freitty.Domain.Common;

namespace Freitty.Application.Common;

// The refusals the aggregate cannot make itself: an order that was never loaded cannot refuse anything.
public static class ApplicationErrors
{
    public static DomainError OrderNotFound(string orderNumber)
    {
        return DomainError.NotFound("order.not_found", $"Order {orderNumber} not found.");
    }
}
