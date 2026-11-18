using FastEndpoints.OpenApi;
using Scalar.AspNetCore;

namespace Freitty.Api.OpenApi;

// The OpenAPI document is generated from the endpoints and shown with Scalar, in every environment: the deployed
// demo shows them.
internal static class ApiDocumentation
{
    private const string DocumentName = "v1";
    private const int DocumentedVersion = 1;
    private const string Title = "Freitty Cabinet API";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.OpenApiDocument(options =>
        {
            options.DocumentName = DocumentName;
            options.Title = Title;
            options.Version = DocumentName;

            // The default of 0 lists no versioned endpoint.
            options.MaxEndpointVersion = DocumentedVersion;
            options.ShortSchemaNames = true;
            options.ExcludeNonFastEndpoints = true;
            options.EnableJWTBearerAuth = false;
        });
        return services;
    }

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options => options.WithTitle(Title));
        return app;
    }
}
