using Freitty.Api.Endpoints;
using Freitty.Api.Health;
using Freitty.Api.OpenApi;
using Microsoft.Net.Http.Headers;

namespace Freitty.Api.Pages;

// A browser that asks for an address with no page behind it gets a page that says so, not a ProblemDetails it would
// show as raw JSON. The API, the health checks, the API reference and every client that does not prefer HTML keep
// the ProblemDetails.
internal static class NotFoundPage
{
    private const string FileName = "404.html";
    private const string HtmlMediaType = "text/html";
    private const string HtmlContentType = "text/html; charset=utf-8";

    private static readonly PathString ApiRoot = $"/{ApiEndpoints.RoutePrefix}";

    public static bool IsFor(HttpContext context)
    {
        HttpRequest request = context.Request;
        return context.Response.StatusCode == StatusCodes.Status404NotFound
            && !request.Path.StartsWithSegments(ApiRoot)
            && !HealthEndpoints.IsHealthRequest(request)
            && !ApiDocumentation.IsDocumentationRequest(request)
            && PrefersHtml(request);
    }

    public static Task WriteAsync(HttpContext context)
    {
        IWebHostEnvironment environment = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
        HttpResponse response = context.Response;
        response.ContentType = HtmlContentType;
        response.Headers.CacheControl = StaticPages.RevalidateEveryTime;
        SecurityHeaders.AddPagePolicy(response);
        return response.SendFileAsync(environment.WebRootFileProvider.GetFileInfo(FileName), context.RequestAborted);
    }

    // The media type the client weighs highest, the first of equals winning, is HTML: what a browser sends for a page,
    // and not what fetch, curl or an HTTP client sends.
    private static bool PrefersHtml(HttpRequest request)
    {
        MediaTypeHeaderValue? preferred = request.GetTypedHeaders().Accept
            .OrderByDescending(mediaType => mediaType.Quality ?? 1)
            .FirstOrDefault();
        return preferred?.MediaType.Equals(HtmlMediaType, StringComparison.OrdinalIgnoreCase) == true;
    }
}
