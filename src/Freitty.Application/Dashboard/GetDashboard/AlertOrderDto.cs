using Freitty.Domain.Orders;

namespace Freitty.Application.Dashboard.GetDashboard;

public sealed record AlertOrderDto(string Number, IReadOnlyList<AlertType> AlertTypes);
