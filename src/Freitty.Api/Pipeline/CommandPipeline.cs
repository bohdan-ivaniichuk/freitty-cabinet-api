using FastEndpoints;
using FluentValidation;
using Freitty.Application;

namespace Freitty.Api.Pipeline;

internal static class CommandPipeline
{
    // FastEndpoints does not register the validators of commands; the order of the middleware is the order here.
    public static IServiceCollection AddCommandPipeline(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddCommandMiddleware(middleware => middleware.Register(
            typeof(CommandLogging<,>),
            typeof(CommandValidation<,>)));
        return services;
    }
}
