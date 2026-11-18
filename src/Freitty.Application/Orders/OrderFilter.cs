using Freitty.Application.Common;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders;

// The filters the list, the tab counters and the export share. A counter equals the total of the list under it, and
// the export holds the orders of the list, because all apply this one class; the company is part of every call, so no
// query can leave it out.
public sealed class OrderFilter(ReportingCalendar calendar, IOrderSearch search)
{
    public IQueryable<Order> Apply(
        IQueryable<Order> orders,
        Guid companyId,
        Guid? hubId,
        DatePreset? datePreset,
        OrderStatus? status,
        string? searchText)
    {
        IQueryable<Order> filtered = orders.Where(order => order.CompanyId == companyId);

        if (hubId is { } hub)
        {
            filtered = filtered.Where(order => order.HubId == hub);
        }

        if (datePreset is { } preset)
        {
            InstantRange range = calendar.RangeOf(preset);
            filtered = filtered.Where(order => order.ScheduledAt >= range.From && order.ScheduledAt < range.To);
        }

        if (status is { } orderStatus)
        {
            filtered = filtered.Where(order => order.Status == orderStatus);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            IQueryable<Guid> matchingIds = search.MatchingOrderIds(companyId, searchText.Trim());
            filtered = filtered.Where(order => matchingIds.Contains(order.Id));
        }

        return filtered;
    }

    // The filters and, on top of them, the tabs of the list: Cross-Dock and Consolidation are a type, Alerts an open
    // alert.
    public IQueryable<Order> ApplyWithTab(
        IQueryable<Order> orders,
        Guid companyId,
        Guid? hubId,
        DatePreset? datePreset,
        OrderStatus? status,
        string? searchText,
        OrderType? type,
        bool hasOpenAlerts)
    {
        IQueryable<Order> filtered = Apply(orders, companyId, hubId, datePreset, status, searchText);
        IQueryable<Order> ofType = type is { } orderType
            ? filtered.Where(order => order.Type == orderType)
            : filtered;
        return hasOpenAlerts ? WithOpenAlert(ofType) : ofType;
    }

    public static IQueryable<Order> WithOpenAlert(IQueryable<Order> orders)
    {
        return orders.Where(order => order.Shipments.Any(shipment =>
            shipment.Alerts.Any(alert => alert.ResolvedAt == null)));
    }
}
