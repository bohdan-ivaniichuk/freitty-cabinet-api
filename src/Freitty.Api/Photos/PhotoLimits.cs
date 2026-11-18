using System.Globalization;
using System.Threading.RateLimiting;
using Freitty.Application.Orders.AttachPhotos;
using Microsoft.AspNetCore.RateLimiting;

namespace Freitty.Api.Photos;

// The demo is public and has no sign-in, so the two routes that move bytes are bounded for everyone together, not per
// client: at most a few uploads at a time, each buffered by the server, and a steady rate of images served. Nothing
// waits in a queue; a request over the bound is answered 429 at once.
internal static class PhotoLimits
{
    public const string UploadPolicy = "photo-uploads";
    public const string ReadPolicy = "photo-reads";
    public const int ConcurrentUploads = 4;
    public const int PhotoReadsPerWindow = 120;

    // Five files at the limit of their size, and room for the multipart framing around them.
    public const long MaxUploadBodyBytes =
        (AttachPhotosCommandValidator.MaxFileCount * PhotoFileValidator.MaxSizeBytes) + UploadFramingBytes;

    private const long UploadFramingBytes = 1024 * 1024;
    private const string TooManyRequestsType = "https://tools.ietf.org/html/rfc6585#section-4";
    private const string RejectedDetail = "The demo is busy with photos right now; try again in a moment.";

    public static readonly TimeSpan PhotoReadWindow = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddPhotoLimits(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PhotoUploadSettings>(configuration.GetSection(PhotoUploadSettings.Section));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddConcurrencyLimiter(UploadPolicy, limiter =>
            {
                limiter.PermitLimit = ConcurrentUploads;
                limiter.QueueLimit = 0;
            });
            options.AddFixedWindowLimiter(ReadPolicy, limiter =>
            {
                limiter.PermitLimit = PhotoReadsPerWindow;
                limiter.Window = PhotoReadWindow;
                limiter.QueueLimit = 0;
            });
            options.OnRejected = WriteRejectionAsync;
        });
        return services;
    }

    // Only the endpoints that name a policy are limited.
    public static WebApplication UsePhotoLimits(this WebApplication app)
    {
        app.UseRateLimiter();
        return app;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        HttpContext httpContext = context.HttpContext;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            httpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        IProblemDetailsService problemDetails =
            httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails =
            {
                Type = TooManyRequestsType,
                Status = StatusCodes.Status429TooManyRequests,
                Detail = RejectedDetail,
            },
        });
    }
}
