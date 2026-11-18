namespace Freitty.Application.Orders.AttachPhotos;

// What a file is, by the first bytes of its content: a client names any type it likes, and a text file renamed to
// .jpg is still text.
public static class PhotoSignature
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string WebP = "image/webp";

    // RIFF, four bytes of length, then WEBP: the longest signature read here.
    private const int HeaderLength = 12;
    private const int WebPFormatOffset = 8;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RiffSignature = "RIFF"u8.ToArray();
    private static readonly byte[] WebPFormat = "WEBP"u8.ToArray();

    // Null when the content is none of JPEG, PNG and WebP.
    public static async Task<string?> ReadContentTypeAsync(PhotoFile file, CancellationToken cancellationToken)
    {
        byte[] header = new byte[HeaderLength];
        await using Stream content = file.OpenReadStream();
        int headerLength =
            await content.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        return ContentTypeOf(header.AsSpan(0, headerLength));
    }

    private static string? ContentTypeOf(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegSignature))
        {
            return Jpeg;
        }

        if (header.StartsWith(PngSignature))
        {
            return Png;
        }

        bool isWebP = header.Length == HeaderLength
            && header.StartsWith(RiffSignature)
            && header[WebPFormatOffset..].SequenceEqual(WebPFormat);
        return isWebP ? WebP : null;
    }
}
