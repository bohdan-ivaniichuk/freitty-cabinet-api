using Freitty.Api.Endpoints;

namespace Freitty.Api.IntegrationTests.Support;

// The URLs the tests request, built from the routes the endpoints declare and the prefix they are served under:
// a route that is renamed is renamed here with it.
internal static class ApiUrls
{
    // Outside the API, where the container runtime probes it.
    public const string Liveness = "/health/live";

    private static readonly string Root =
        $"/{ApiEndpoints.RoutePrefix}/{ApiEndpoints.VersionPrefix}{ApiEndpoints.CurrentVersion}";

    private static readonly string NumberPlaceholder = $"{{{ApiRoutes.Orders.NumberParameter}}}";

    public static string Orders { get; } = Of(ApiRoutes.Orders.List);

    public static string OrderCounts { get; } = Of(ApiRoutes.Orders.Counts);

    public static string OrderExport { get; } = Of(ApiRoutes.Orders.Export);

    public static string OrderPipeline { get; } = Of(ApiRoutes.Orders.Pipeline);

    // The routes of one order as the OpenAPI document lists them, with the number still a placeholder.
    public static string OrderTemplate { get; } = Of(ApiRoutes.Orders.Detail);

    public static string OperationsTemplate { get; } = Of(ApiRoutes.Orders.Operations);

    public static string SubmitTemplate { get; } = Of(ApiRoutes.Orders.Submit);

    public static string Dashboard { get; } = Of(ApiRoutes.Dashboard.Get);

    public static string Hubs { get; } = Of(ApiRoutes.Hubs.List);

    public static string Me { get; } = Of(ApiRoutes.Me.Get);

    public static string Order(string orderNumber)
    {
        return OrderTemplate.Replace(NumberPlaceholder, orderNumber, StringComparison.Ordinal);
    }

    public static string Operations(string orderNumber)
    {
        return OperationsTemplate.Replace(NumberPlaceholder, orderNumber, StringComparison.Ordinal);
    }

    public static string Submit(string orderNumber)
    {
        return SubmitTemplate.Replace(NumberPlaceholder, orderNumber, StringComparison.Ordinal);
    }

    public static string Of(string route)
    {
        return $"{Root}/{route}";
    }
}
