using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Freitty.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Api.IntegrationTests.Support;

// What the photo storage holds under uploads/. Every API of the run shares it, so a test compares it only within the
// photo storage collection.
internal static class StoredUploads
{
    public static async Task<string[]> ListAsync(IServiceProvider services)
    {
        BlobContainerClient container = services.GetRequiredService<BlobContainerClient>();
        var uploads = new List<string>();
        var uploadsOnly = new GetBlobsOptions { Prefix = PhotoStorageKeys.UploadsPrefix };
        await foreach (BlobItem upload in container.GetBlobsAsync(uploadsOnly, TestContext.Current.CancellationToken))
        {
            uploads.Add(upload.Name);
        }

        return [.. uploads];
    }
}
