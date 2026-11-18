using Freitty.Application.Common;
using Freitty.Application.Orders;

namespace Freitty.Api.Endpoints.Orders;

// What the OpenAPI document says about the filters the list, the tab counters and the export share.
internal static class OrderFilterDescriptions
{
    public const string Type = "CrossDock or Consolidation, the tabs of those names; every type when left out.";

    public const string Status = "The Status filter; Draft is the Drafts tab. Every status when left out.";

    public const string HasOpenAlerts =
        "True keeps the orders with an open alert on any shipment, the Alerts tab; false when left out.";

    public const string HubId = "The id of a hub, as the list of hubs returns it; every hub when left out.";

    public static readonly string DatePreset =
        "Orders by their Scheduled date, in the reporting time zone: Today, ThisWeek (from Monday to Sunday) or " +
        $"Last30Days (today and the {ReportingCalendar.DaysInLast30Days - 1} days before it); any date when left out.";

    public static readonly string Search =
        "Text to find, case-insensitively, in order numbers and in the Ref N of shipments; a sub-order label " +
        $"such as FR001674-2 finds its order. From {FilterRules.MinSearchLength} to " +
        $"{FilterRules.MaxSearchLength} characters; no search when left out or blank.";
}
