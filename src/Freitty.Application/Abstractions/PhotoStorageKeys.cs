namespace Freitty.Application.Abstractions;

// An upload and a sample image live under prefixes of their own, so a rebuild of the sample data can delete every
// upload and keep the samples.
public static class PhotoStorageKeys
{
    public const string UploadsPrefix = "uploads/";
    public const string SamplesPrefix = "samples/";

    // A UUID of its own, not the photo's id: the bytes are named before the aggregate creates the photo.
    public static string Upload(Guid uploadId)
    {
        return $"{UploadsPrefix}{uploadId}";
    }

    public static string Sample(string name)
    {
        return $"{SamplesPrefix}{name}";
    }
}
