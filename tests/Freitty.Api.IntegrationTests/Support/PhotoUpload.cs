using System.Globalization;
using System.Net.Http.Headers;

namespace Freitty.Api.IntegrationTests.Support;

// A file of an upload as a browser sends it: its name, the type the browser claims and the bytes. The bytes start
// with the signature of their format and go on with bytes of this upload alone, so a photo read back is known by
// them.
internal sealed record PhotoUpload(string FileName, byte[] Content, string ContentType)
{
    public const string FormField = "files";
    public const string VersionFormField = "version";
    public const int DefaultSizeBytes = 2048;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF, 0xE0];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] WebPSignature = "RIFF\0\0\0\0WEBP"u8.ToArray();

    public static PhotoUpload Png(string fileName = "dock.png", int sizeBytes = DefaultSizeBytes)
    {
        return new PhotoUpload(fileName, Signed(PngSignature, sizeBytes), "image/png");
    }

    public static PhotoUpload Jpeg(string fileName = "dock.jpg", int sizeBytes = DefaultSizeBytes)
    {
        return new PhotoUpload(fileName, Signed(JpegSignature, sizeBytes), "image/jpeg");
    }

    public static PhotoUpload WebP(string fileName = "dock.webp", int sizeBytes = DefaultSizeBytes)
    {
        return new PhotoUpload(fileName, Signed(WebPSignature, sizeBytes), "image/webp");
    }

    // A text file renamed: the name and the claimed type say JPEG, the bytes do not.
    public static PhotoUpload TextNamedJpeg(string fileName = "notes.jpg")
    {
        return new PhotoUpload(fileName, "Counted 12 pallets on arrival."u8.ToArray(), "image/jpeg");
    }

    public static PhotoUpload[] Pngs(int count)
    {
        return Enumerable.Range(1, count).Select(index => Png($"dock-{index}.png")).ToArray();
    }

    // The form the page sends: the version, left out when null, and the files under one field name.
    public static MultipartFormDataContent Form(int? version, params PhotoUpload[] files)
    {
        var form = new MultipartFormDataContent();
        if (version is { } versionSeen)
        {
            form.Add(new StringContent(versionSeen.ToString(CultureInfo.InvariantCulture)), VersionFormField);
        }

        foreach (PhotoUpload file in files)
        {
            file.AddTo(form);
        }

        return form;
    }

    public void AddTo(MultipartFormDataContent form)
    {
        var file = new ByteArrayContent(Content);
        file.Headers.ContentType = new MediaTypeHeaderValue(ContentType);
        form.Add(file, FormField, FileName);
    }

    private static byte[] Signed(byte[] signature, int sizeBytes)
    {
        byte[] content = new byte[sizeBytes];
        Random.Shared.NextBytes(content);
        signature.CopyTo(content, 0);
        return content;
    }
}
