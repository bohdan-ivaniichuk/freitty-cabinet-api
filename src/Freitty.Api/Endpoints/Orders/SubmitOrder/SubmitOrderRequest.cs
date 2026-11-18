namespace Freitty.Api.Endpoints.Orders.SubmitOrder;

// The JSON body. The draft number comes from the route.
internal sealed record SubmitOrderRequest(int ExpectedVersion);
