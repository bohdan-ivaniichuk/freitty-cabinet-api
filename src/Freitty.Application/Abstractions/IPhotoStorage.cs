namespace Freitty.Application.Abstractions;

// Where the bytes of the photos are kept, by the keys of PhotoStorageKeys; the database keeps a row per photo. A key
// that holds nothing is not an error: it opens as null and its deletion does nothing.
public interface IPhotoStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    // Null when the key holds nothing. The caller disposes the stream.
    Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken);

    // Every uploaded photo, the sample images aside.
    Task DeleteUploadsAsync(CancellationToken cancellationToken);
}
