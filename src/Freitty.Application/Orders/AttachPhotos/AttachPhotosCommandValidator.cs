using FluentValidation;

namespace Freitty.Application.Orders.AttachPhotos;

// The shape of the request, nothing about the state of the order: the number of files, and what each one is. The
// files are checked only when there are not too many, so a request of a hundred files is not read a hundred times.
public sealed class AttachPhotosCommandValidator : AbstractValidator<AttachPhotosCommand>
{
    public const int MaxFileCount = 5;

    public AttachPhotosCommandValidator()
    {
        RuleFor(command => command.Version).GreaterThanOrEqualTo(1);
        RuleFor(command => command.Files)
            .NotEmpty()
            .WithMessage("Choose at least one photo.")
            .Must(files => files.Count <= MaxFileCount)
            .WithMessage(command =>
                $"At most {MaxFileCount} photos can be uploaded at once; {command.Files.Count} were sent.");
        RuleForEach(command => command.Files)
            .SetValidator(new PhotoFileValidator())
            .When(command => command.Files.Count <= MaxFileCount);
    }
}
