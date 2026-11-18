using Freitty.Domain.Orders;

namespace Freitty.Application.Dashboard;

public sealed record AlertOrderDto(string Number, IReadOnlyList<AlertType> AlertTypes);
