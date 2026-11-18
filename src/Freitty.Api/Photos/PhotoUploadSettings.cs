namespace Freitty.Api.Photos;

// Photos:UploadsEnabled turns the uploads of the demo off without a new release; reading photos is not affected. It is
// read through options at request time.
internal sealed class PhotoUploadSettings
{
    public const string Section = "Photos";

    public bool UploadsEnabled { get; set; } = true;
}
