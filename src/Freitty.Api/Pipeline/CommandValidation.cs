using System.Text.Json;
using FastEndpoints;
using FluentValidation;
using FluentValidation.Results;

namespace Freitty.Api.Pipeline;

// Runs the FluentValidation validators of the query or command, so a failure never reaches the handler. The
// failures go through FastEndpoints' validation context to leave as its standard 400; FluentValidation's own
// exception would be a 500.
internal sealed class CommandValidation<TCommand, TResult>(IEnumerable<IValidator<TCommand>> validators)
    : ICommandMiddleware<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private const char PathSeparator = '.';

    // Read once: outside a request, every read of ValidationContext.Instance is a new, empty context, so failures
    // added to one and checked on another would let invalid input through.
    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        ValidationContext validation = ValidationContext.Instance;
        foreach (IValidator<TCommand> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(command, ct);
            foreach (ValidationFailure failure in result.Errors)
            {
                validation.AddError(new ValidationFailure(FieldName(failure.PropertyName), failure.ErrorMessage));
            }
        }

        validation.ThrowIfAnyErrors();
        return await next();
    }

    // The field as the request spells it: every segment of a path camelCased, an indexer kept as it is
    // ("Lines[0].UnitPrice" becomes "lines[0].unitPrice").
    private static string FieldName(string propertyPath)
    {
        IEnumerable<string> segments = propertyPath
            .Split(PathSeparator)
            .Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment));
        return string.Join(PathSeparator, segments);
    }
}
