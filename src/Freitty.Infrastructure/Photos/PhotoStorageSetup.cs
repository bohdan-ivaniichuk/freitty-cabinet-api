using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;

namespace Freitty.Infrastructure.Photos;

public static class PhotoStorageSetup
{
    // Runs before the migrations, which it does not depend on, and before the seed, which uploads the sample images
    // into it. The container is private: the API serves the photos itself. A storage that cannot be reached stops the
    // startup, as an unreachable database does.
    public static async Task PreparePhotoStorageAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken)
    {
        BlobContainerClient container = services.GetRequiredService<BlobContainerClient>();
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
    }
}
