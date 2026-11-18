namespace Freitty.Api.Endpoints.Orders.AttachPhotos;

// The multipart form: the fields named version and files. The order number and the operation come from the route.
// Files left out bind as none, which the validator refuses.
internal sealed record AttachPhotosRequest
{
    public int Version { get; init; }

    public IFormFileCollection Files { get; init; } = new FormFileCollection();
}
