namespace Freitty.Api.Endpoints;

// The route of every endpoint, relative to /api/v1 (the prefix and the version are set in ApiEndpoints).
internal static class ApiRoutes
{
    public static class Orders
    {
        public const string NumberParameter = "number";
        public const string OperationIdParameter = "operationId";
        public const string PhotoIdParameter = "photoId";

        public const string List = "orders";
        public const string Counts = "orders/counts";
        public const string Export = "orders/export";
        public const string Pipeline = "orders/pipeline";
        public const string Detail = $"orders/{{{NumberParameter}}}";
        public const string Operations = $"orders/{{{NumberParameter}}}/operations";
        public const string Submit = $"orders/{{{NumberParameter}}}/submit";
        public const string Photos = $"orders/{{{NumberParameter}}}/photos";
        public const string Photo = $"orders/{{{NumberParameter}}}/photos/{{{PhotoIdParameter}}}";
        public const string OperationPhotos =
            $"orders/{{{NumberParameter}}}/operations/{{{OperationIdParameter}}}/photos";
    }

    public static class Dashboard
    {
        public const string Get = "dashboard";
    }

    public static class Hubs
    {
        public const string List = "hubs";
    }

    public static class Me
    {
        public const string Get = "me";
    }
}
