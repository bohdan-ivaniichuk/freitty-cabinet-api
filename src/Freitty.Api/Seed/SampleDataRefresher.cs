using Freitty.Infrastructure.Persistence.Seed;

namespace Freitty.Api.Seed;

// Keeps the demo's sample data dated from today: every CheckInterval it checks the anchor day and, once it has moved
// on, rebuilds the data behind the closed request gate. A failed rebuild is logged and tried again at the next tick;
// the loop ends only with the application.
internal sealed class SampleDataRefresher(
    SampleData sampleData,
    RequestGate requestGate,
    TimeProvider clock,
    ILogger<SampleDataRefresher> logger)
    : BackgroundService
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    // How long the closed gate waits for the requests in flight. A request that hangs fails the check instead of
    // holding every other request; the next tick tries again.
    public static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(10);

    // Created with the service, not when the loop starts on its own thread, so the first tick is due one interval
    // after the host started whichever runs first.
    private readonly PeriodicTimer timer = new(CheckInterval, clock);

    public override void Dispose()
    {
        timer.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckAsync(stoppingToken);
        }
    }

    private async Task CheckAsync(CancellationToken stoppingToken)
    {
        if (!sampleData.IsOutdated)
        {
            return;
        }

        try
        {
            using var drainDeadline = new CancellationTokenSource(DrainTimeout, clock);
            using var drainCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, drainDeadline.Token);
            using IDisposable closedGate = await requestGate.CloseAsync(drainCancellation.Token);
            await sampleData.RebuildAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Rebuilding the sample data failed; it is tried again in {CheckInterval}",
                CheckInterval);
        }
    }
}
