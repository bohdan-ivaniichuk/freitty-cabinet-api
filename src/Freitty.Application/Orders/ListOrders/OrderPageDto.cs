using Freitty.Application.Orders.Cards;

namespace Freitty.Application.Orders.ListOrders;

public sealed record OrderPageDto(IReadOnlyList<OrderCardDto> Items, int Page, int PageSize, int Total);
