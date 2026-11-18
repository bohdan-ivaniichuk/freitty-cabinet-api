using Freitty.Api.Seed;

namespace Freitty.Api.IntegrationTests.Seed;

// Pure unit tests, kept here because the gate is internal to Api, whose internals only this project sees.
public sealed class RequestGateTests
{
    private readonly RequestGate gate = new();

    [Fact]
    public async Task ClosingWaitsForTheRequestsInFlight()
    {
        IDisposable entered = await gate.EnterAsync(CancellationToken);

        Task<IDisposable> closing = gate.CloseAsync(CancellationToken);
        Assert.False(closing.IsCompleted);
        entered.Dispose();

        using IDisposable closed = await closing;
    }

    [Fact]
    public async Task RequestWaitsUntilTheGateOpens()
    {
        IDisposable closed = await gate.CloseAsync(CancellationToken);

        Task<IDisposable> entering = gate.EnterAsync(CancellationToken);
        Assert.False(entering.IsCompleted);
        closed.Dispose();

        using IDisposable entered = await entering;
    }

    [Fact]
    public async Task CancelledClosingLeavesTheGateOpen()
    {
        using IDisposable inFlight = await gate.EnterAsync(CancellationToken);
        using var cancellation = new CancellationTokenSource();

        Task<IDisposable> closing = gate.CloseAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => closing);
        Task<IDisposable> entering = gate.EnterAsync(CancellationToken);
        Assert.True(entering.IsCompletedSuccessfully);
        (await entering).Dispose();
    }

    [Fact]
    public async Task RequestCancelledAtTheClosedGateIsNotInFlight()
    {
        IDisposable closed = await gate.CloseAsync(CancellationToken);
        using var cancellation = new CancellationTokenSource();
        Task<IDisposable> entering = gate.EnterAsync(cancellation.Token);

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => entering);
        closed.Dispose();

        Task<IDisposable> closingAgain = gate.CloseAsync(CancellationToken);
        Assert.True(closingAgain.IsCompletedSuccessfully);
        (await closingAgain).Dispose();
    }

    [Fact]
    public async Task ClosingAClosedGateIsRefused()
    {
        using IDisposable closed = await gate.CloseAsync(CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gate.CloseAsync(CancellationToken));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;
}
