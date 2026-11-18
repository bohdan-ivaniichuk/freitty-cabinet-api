namespace Freitty.Api.Pages;

// Headers a browser acts on. Two go on every response; the content security policy goes only on the pages of
// wwwroot, which load nothing but their own scripts, styles and images and call only their own API. The API
// reference at /scalar loads assets of its own and keeps the browser's defaults.
internal static class SecurityHeaders
{
    // The pages set style attributes throughout, so inline styles are allowed; inline scripts are not.
    public const string PageContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self'; " +
        "connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    private const string NoSniff = "nosniff";
    private const string NoReferrer = "no-referrer";

    // Outermost, and set as the response starts: the exception handler clears the headers of a failed response.
    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.XContentTypeOptions = NoSniff;
                context.Response.Headers["Referrer-Policy"] = NoReferrer;
                return Task.CompletedTask;
            });
            return next(context);
        });
        return app;
    }

    public static void AddPagePolicy(HttpResponse response)
    {
        response.Headers.ContentSecurityPolicy = PageContentSecurityPolicy;
    }
}
