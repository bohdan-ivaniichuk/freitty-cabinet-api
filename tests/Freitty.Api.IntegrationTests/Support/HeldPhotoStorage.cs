using Freitty.Application.Abstractions;

namespace Freitty.Api.IntegrationTests.Support;

// A photo storage that holds every save until the test releases it, and counts the saves that are waiting: an upload
// that reaches it is inside the API's limits, so a test knows how many are in flight. Nothing is stored.
internal sealed class HeldPhotoStorage : IPhotoStorage
{
    private static readonly TimeSpan EnterBound = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim entered = new(0);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        entered.Release();
        await released.Task.WaitAsync(cancellationToken);
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        return Task.FromResult<Stream?>(null);
    }

    public Task DeleteAsync(IEnumerable<string> keys, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task DeleteUploadsAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    // The bound only ends a test that would otherwise hang.
    public async Task WaitUntilHeldAsync(int saveCount)
    {
        for (int save = 0; save < saveCount; save++)
        {
            Assert.True(
                await entered.WaitAsync(EnterBound, TestContext.Current.CancellationToken),
                $"Only {save} of {saveCount} saves reached the storage.");
        }
    }

    public void Release()
    {
        released.TrySetResult();
    }
}
