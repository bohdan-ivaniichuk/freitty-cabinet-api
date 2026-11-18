using FastEndpoints;
using Freitty.Domain.Common;

namespace Freitty.Api.Pipeline;

// The outer step of every query and command: what ran, how long it took and how it ended — a refusal of the domain
// with its code. A thrown exception is only named here; the request log records it with its stack trace, once.
internal sealed class CommandLogging<TCommand, TResult>(ILoggerFactory loggerFactory, TimeProvider clock)
    : ICommandMiddleware<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    private const string Category = "Freitty.Api.Pipeline.CommandLogging";
    private const string Completed = "completed";
    private const string WasRefused = "was refused with";
    private const string RejectedByValidation = "was rejected by validation";
    private const string WasCancelled = "was cancelled";
    private const string Threw = "threw";

    private readonly ILogger logger = loggerFactory.CreateLogger(Category);

    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        long startedAt = clock.GetTimestamp();
        string outcome = Threw;
        try
        {
            TResult result = await next();
            outcome = result is IDomainResult { Error: { } error } ? $"{WasRefused} {error.Code}" : Completed;
            return result;
        }
        catch (ValidationFailureException)
        {
            outcome = RejectedByValidation;
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            outcome = WasCancelled;
            throw;
        }
        finally
        {
            logger.LogInformation(
                "{Command} {Outcome} in {ElapsedMilliseconds:0.0} ms",
                typeof(TCommand).Name,
                outcome,
                clock.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }
}
