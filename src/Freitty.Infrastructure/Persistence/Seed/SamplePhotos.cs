using Freitty.Application.Abstractions;
using Freitty.Application.Orders.AttachPhotos;
using Freitty.Domain.Orders;

namespace Freitty.Infrastructure.Persistence.Seed;

// The pictures a seeded photo points at: placeholders drawn by Images/draw_sample_photos.py and embedded in this
// assembly. Every seeded photo is a row that names one of them, so the seeded counts and the galleries agree. They
// are uploaded every time, a few kilobytes: a storage that kept older pictures would otherwise serve bytes of
// another length than the rows say. A rebuild never deletes them.
internal static class SamplePhotos
{
    // The logical name the project file gives each embedded image.
    private const string ResourcePrefix = "SamplePhotos.";

    private static readonly string[] Names =
        ["pallets-on-dock.png", "trailer-at-door.png", "damaged-pallet.png", "wrapped-stack.png"];

    public static IReadOnlyList<NewPhoto> Photos { get; } = Names.Select(Describe).ToArray();

    public static async Task UploadAsync(IPhotoStorage photoStorage, CancellationToken cancellationToken)
    {
        foreach (string name in Names)
        {
            await using Stream content = OpenResource(name);
            await photoStorage.SaveAsync(PhotoStorageKeys.Sample(name), content, PhotoSignature.Png, cancellationToken);
        }
    }

    private static NewPhoto Describe(string name)
    {
        using Stream content = OpenResource(name);
        return new NewPhoto(PhotoStorageKeys.Sample(name), PhotoSignature.Png, content.Length);
    }

    private static Stream OpenResource(string name)
    {
        return typeof(SamplePhotos).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException(
                $"The sample image {name} is not embedded; check the EmbeddedResource item of the project file.");
    }
}
