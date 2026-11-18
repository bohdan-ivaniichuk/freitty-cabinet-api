namespace Freitty.Api.Pages;

// The cabinet's three pages and their scripts are static files in wwwroot, served on the API's origin
// (ADR 0012). Used before the request log, so an asset is not logged one line each; a path that is no
// file falls through to the endpoints and, unmatched, to the 404 ProblemDetails.
internal static class StaticPages
{
    // Revalidated on every load: a new deploy is picked up instead of a script cached by the browser.
    private const string RevalidateEveryTime = "no-cache";

    public static WebApplication UseStaticPages(this WebApplication app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = RevalidateEveryTime,
        });
        return app;
    }
}
