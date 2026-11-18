using Freitty.Application.Common;
using Freitty.Application.Orders;
using Freitty.Application.Orders.Cards;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Application;

// Handlers are found and created by the command bus; these are the services they share. Scoped, like the database
// context they work with.
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ReportingCalendar>();
        services.AddScoped<OrderFilter>();
        services.AddScoped<OrderCardReader>();
        return services;
    }
}
