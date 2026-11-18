namespace Freitty.Api.Pages;

// The cabinet's three pages and their scripts are static files in wwwroot, served on the API's origin
// (ADR 0012). Used before the request log, so an asset is not logged one line each; a path that is no
// file falls through to the endpoints and, unmatched, to the 404 of ErrorResponses.
internal static class StaticPages
{
    // Revalidated on every load: a new deploy is picked up instead of a script cached by the browser.
    public const string RevalidateEveryTime = "no-cache";

    private const string HtmlExtension = ".html";

    // The two pages a visitor is likely to type without the extension. The query string goes along, so a link with
    // a filter or an order number still opens what it named.
    private static readonly Dictionary<PathString, string> PageAddresses = new()
    {
        ["/orders"] = "/orders.html",
        ["/order"] = "/order.html",
    };

    public static WebApplication UseStaticPages(this WebApplication app)
    {
        app.Use((context, next) =>
        {
            if (PageAddresses.TryGetValue(context.Request.Path, out string? page))
            {
                context.Response.Redirect(page + context.Request.QueryString);
                return Task.CompletedTask;
            }

            return next(context);
        });
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = RevalidateEveryTime;
                if (context.File.Name.EndsWith(HtmlExtension, StringComparison.Ordinal))
                {
                    SecurityHeaders.AddPagePolicy(context.Context.Response);
                }
            },
        });
        return app;
    }
}
