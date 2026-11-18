using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FluentValidation.Results;

namespace Freitty.Api.Endpoints;

// FastEndpoints under /api/v1. Enums travel as strings, both in what FastEndpoints serializes and in what
// ASP.NET Core does (ProblemDetails, the OpenAPI schemas).
internal static class ApiEndpoints
{
    public const string RoutePrefix = "api";
    public const string VersionPrefix = "v";
    public const int CurrentVersion = 1;

    // The name a binding failure of the whole body is reported under: no single field is at fault.
    private const string WholeBodyField = "serializerErrors";
    private const string JsonPathRoot = "$";
    private const string InvalidBody = "The request body is not valid JSON for this endpoint.";
    private const string InvalidValue = "The value is not valid for this field.";

    // The type ASP.NET Core gives a 400, so that every ProblemDetails of the API names its status the same way.
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    public static IServiceCollection AddApiEndpoints(this IServiceCollection services)
    {
        services.AddFastEndpoints();
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }

    public static WebApplication UseApiEndpoints(this WebApplication app)
    {
        app.UseFastEndpoints(config =>
        {
            config.Endpoints.RoutePrefix = RoutePrefix;

            // Operation ids are the endpoint class names, not their full names with the namespace.
            config.Endpoints.ShortNames = true;

            // There is no authentication yet: ICurrentUser stands in for it.
            config.Endpoints.Configurator = endpoint => endpoint.AllowAnonymous();

            // Prepended, since the default appends the version: /api/orders/v1.
            config.Versioning.Prefix = VersionPrefix;
            config.Versioning.PrependToRoute = true;
            config.Versioning.DefaultVersion = CurrentVersion;

            // FastEndpoints fills the detail only when there is one error; the pages show the first in any case.
            config.Errors.UseProblemDetails(problem =>
            {
                problem.TypeTransformer = _ => BadRequestType;
                problem.DetailTransformer = details => details.Errors.FirstOrDefault()?.Reason;
            });

            // System.Text.Json's and the binder's own messages name .NET types; the field name, which a client can
            // act on, stays in errors[].name.
            config.Binding.JsonExceptionTransformer = exception => IsAboutTheWholeBody(exception)
                ? new ValidationFailure(WholeBodyField, InvalidBody)
                : new ValidationFailure(exception.Path![2..], InvalidValue);
            config.Binding.FailureMessage = (_, _, _) => InvalidValue;

            // Validators run in the command pipeline, which camelCases the field names itself. Left on, this
            // renames FluentValidation's properties globally, and its messages would read "'page Size' must be …".
            config.Validation.UsePropertyNamingPolicy = false;
            config.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
        });
        return app;
    }

    // No field is at fault when the body is not an object, or is not JSON at all: text the reader cannot parse
    // carries the reader's own exception, whatever field it stopped in.
    private static bool IsAboutTheWholeBody(JsonException exception)
    {
        return exception.Path is null or JsonPathRoot || exception.InnerException is JsonException;
    }
}
