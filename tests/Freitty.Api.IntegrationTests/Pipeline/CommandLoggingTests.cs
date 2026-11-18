using FastEndpoints;
using Freitty.Api.Pipeline;
using Freitty.Application.Orders;
using Freitty.Domain.Common;
using Microsoft.Extensions.Logging;

namespace Freitty.Api.IntegrationTests.Pipeline;

// The middleware is run on its own. Every API host of a test process writes through one static Serilog logger, so
// a line read from the log of a host could be another test's.
public sealed class CommandLoggingTests : IDisposable
{
    private readonly RecordedLog log = new();
    private readonly ILoggerFactory loggerFactory;
    private readonly CommandLogging<Probe, Result<string>> logging;

    public CommandLoggingTests()
    {
        loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(log));
        logging = new CommandLogging<Probe, Result<string>>(loggerFactory, TimeProvider.System);
    }

    [Fact]
    public async Task CommandThatSucceedsIsLoggedAsCompleted()
    {
        Result<string> result = await logging.ExecuteAsync(
            new Probe(),
            () => Task.FromResult<Result<string>>("done"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("Probe completed in ", Assert.Single(log.Lines));
    }

    [Fact]
    public async Task CommandTheDomainRefusesIsLoggedWithTheCodeOfTheRefusal()
    {
        Result<string> result = await logging.ExecuteAsync(
            new Probe(),
            () => Task.FromResult<Result<string>>(OrderRequestErrors.OrderNotFound("FR000001")),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.StartsWith(
            $"Probe was refused with {OrderRequestErrors.OrderNotFoundCode} in ",
            Assert.Single(log.Lines));
    }

    [Fact]
    public async Task CommandThatThrowsIsLoggedAsThrownAndTheExceptionGoesOn()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => logging.ExecuteAsync(
            new Probe(),
            () => throw new InvalidOperationException("The handler failed."),
            TestContext.Current.CancellationToken));

        Assert.StartsWith("Probe threw in ", Assert.Single(log.Lines));
    }

    [Fact]
    public async Task CommandOfARequestThatWasAbandonedIsLoggedAsCancelled()
    {
        using var abandoned = new CancellationTokenSource();
        await abandoned.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => logging.ExecuteAsync(
            new Probe(),
            () => throw new OperationCanceledException(abandoned.Token),
            abandoned.Token));

        Assert.StartsWith("Probe was cancelled in ", Assert.Single(log.Lines));
    }

    public void Dispose()
    {
        loggerFactory.Dispose();
    }

    private sealed record Probe : ICommand<Result<string>>;
}
