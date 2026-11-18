using FluentValidation;

namespace Freitty.Application.Orders.AttachPhotos;

// Each message names the file, so the client can tell which of several was refused.
public sealed class PhotoFileValidator : AbstractValidator<PhotoFile>
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;
    public const string MaxSizeText = "5 MB";

    public PhotoFileValidator()
    {
        RuleFor(file => file)
            .Cascade(CascadeMode.Stop)
            .Must(file => file.SizeBytes <= MaxSizeBytes)
            .WithMessage(file => $"{file.FileName} is larger than {MaxSizeText}.")
            .MustAsync(async (file, cancellationToken) =>
                await PhotoSignature.ReadContentTypeAsync(file, cancellationToken) is not null)
            .WithMessage(file => $"{file.FileName} is not a JPEG, PNG or WebP image.");
    }
}
