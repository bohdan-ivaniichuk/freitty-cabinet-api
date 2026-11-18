using Freitty.Application.Common;
using Freitty.Application.Orders;

namespace Freitty.Api.Endpoints.Orders;

// What the OpenAPI document says about the filters the list and the tab counters share.
internal static class OrderFilterDescriptions
{
    public const string HubId = "The id of a hub, as the list of hubs returns it; every hub when left out.";

    public static readonly string DatePreset =
        "Orders by their Scheduled date, in the reporting time zone: Today, ThisWeek (from Monday to Sunday) or " +
        $"Last30Days (today and the {ReportingCalendar.DaysInLast30Days - 1} days before it); any date when left out.";

    public static readonly string Search =
        "Text to find, case-insensitively, in order numbers and in the Ref N of shipments; a sub-order label " +
        $"such as FR001674-2 finds its order. From {FilterRules.MinSearchLength} to " +
        $"{FilterRules.MaxSearchLength} characters; no search when left out or blank.";
}
