namespace Freitty.Application.Orders;

// Where the freight of an order is. Unlike the status, it tells the hub's work apart; alerts do not change it.
public enum OrderStage
{
    Draft = 1,
    AwaitingTruck = 2,
    Unloading = 3,
    Loading = 4,
    AwaitingCompletion = 5,
    Completed = 6,
}
