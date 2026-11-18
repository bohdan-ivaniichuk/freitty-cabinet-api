using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

internal static class OrderQueries
{
    // The whole number, compared in lower case: a pattern match would make a "_" in the route a wildcard.
    public static IQueryable<Order> WithNumber(this IQueryable<Order> orders, Guid companyId, string orderNumber)
    {
        string lowerCaseNumber = orderNumber.ToLowerInvariant();
        return orders.Where(order => order.CompanyId == companyId && order.Number.ToLower() == lowerCaseNumber);
    }
}
