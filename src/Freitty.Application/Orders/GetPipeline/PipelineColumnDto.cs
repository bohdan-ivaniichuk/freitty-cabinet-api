using Freitty.Application.Orders.Cards;
using Freitty.Domain.Orders;

namespace Freitty.Application.Orders.GetPipeline;

// Total counts every order of the status under the filters; Orders holds the first of them in the list's order.
public sealed record PipelineColumnDto(OrderStatus Status, int Total, IReadOnlyList<OrderCardDto> Orders);
