using System.Net;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Freitty.Application.Abstractions;

namespace Freitty.Infrastructure.Photos;

// The photos are blobs of one private container: Azure Blob Storage in the deployment, Azurite under Docker Compose
// and in the tests. The API streams them to the pages, so nothing here is ever public.
internal sealed class BlobPhotoStorage(BlobContainerClient container) : IPhotoStorage
{
    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var options = new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } };
        await container.GetBlobClient(key).UploadAsync(content, options, cancellationToken);
    }

    // One request that streams the bytes as they arrive, instead of reading the blob into memory first.
    public async Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            Response<BlobDownloadStreamingResult> download =
                await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return download.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        foreach (string key in keys)
        {
            await container.DeleteBlobIfExistsAsync(key, cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteUploadsAsync(CancellationToken cancellationToken)
    {
        var uploadsOnly = new GetBlobsOptions { Prefix = PhotoStorageKeys.UploadsPrefix };
        AsyncPageable<BlobItem> uploads = container.GetBlobsAsync(uploadsOnly, cancellationToken);
        await foreach (BlobItem upload in uploads)
        {
            await container.DeleteBlobIfExistsAsync(upload.Name, cancellationToken: cancellationToken);
        }
    }
}
