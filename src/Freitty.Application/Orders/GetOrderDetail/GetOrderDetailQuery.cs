using FastEndpoints;
using Freitty.Domain.Common;

namespace Freitty.Application.Orders.GetOrderDetail;

// The number is matched as a whole and case-insensitively, within the current company.
public sealed record GetOrderDetailQuery(string Number) : ICommand<Result<OrderDetailDto>>;
