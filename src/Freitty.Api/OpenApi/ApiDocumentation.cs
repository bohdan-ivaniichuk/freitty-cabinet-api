using FastEndpoints.OpenApi;
using Freitty.Api.Endpoints;
using Scalar.AspNetCore;

namespace Freitty.Api.OpenApi;

// The OpenAPI document is generated from the endpoints and shown with Scalar, in every environment: the deployed
// demo shows them.
internal static class ApiDocumentation
{
    private const string Title = "Freitty Cabinet API";

    // Where MapOpenApi and MapScalarApiReference serve by default.
    private static readonly PathString DocumentRoot = "/openapi";
    private static readonly PathString ReferenceRoot = "/scalar";

    // The document is named for the one version the endpoints are served under.
    private static readonly string DocumentName = $"{ApiEndpoints.VersionPrefix}{ApiEndpoints.CurrentVersion}";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.OpenApiDocument(options =>
        {
            options.DocumentName = DocumentName;
            options.Title = Title;
            options.Version = DocumentName;

            // The default of 0 lists no versioned endpoint.
            options.MaxEndpointVersion = ApiEndpoints.CurrentVersion;
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

    public static bool IsDocumentationRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments(DocumentRoot) || request.Path.StartsWithSegments(ReferenceRoot);
    }
}
