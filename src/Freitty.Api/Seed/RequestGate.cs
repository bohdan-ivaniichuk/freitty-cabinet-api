namespace Freitty.Api.Seed;

// Lets every request through, except while the sample data is rebuilt: closing waits for the requests in flight to
// finish, and a request that arrives while it is closed waits for it to open. The rebuild gives every row a new id, so
// a request whose statements fell on both sides of its commit would look up ids that are gone — a list of cards that
// are missing, a known order not found, a current user that no longer exists. Behind the gate a request reads the old
// data or the new, never some of each. It holds one process: several instances would need the database to agree.
internal sealed class RequestGate
{
    private readonly Lock sync = new();
    private int requestsInFlight;
    private TaskCompletionSource? reopened;
    private TaskCompletionSource? drained;

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task closedUntil;
            lock (sync)
            {
                if (reopened is null)
                {
                    requestsInFlight++;
                    return new Release(Leave);
                }

                closedUntil = reopened.Task;
            }

            await closedUntil.WaitAsync(cancellationToken);
        }
    }

    // Returns once no request is in flight; the gate stays closed until the result is disposed. One closer at a time:
    // a second close would leave the requests waiting on the first never released.
    public async Task<IDisposable> CloseAsync(CancellationToken cancellationToken)
    {
        Task allLeft;
        lock (sync)
        {
            if (reopened is not null)
            {
                throw new InvalidOperationException(
                    "The request gate is already closed; open it before closing it again.");
            }

            reopened = NewSignal();
            drained = NewSignal();
            if (requestsInFlight == 0)
            {
                drained.SetResult();
            }

            allLeft = drained.Task;
        }

        try
        {
            await allLeft.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Open();
            throw;
        }

        return new Release(Open);
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private void Leave()
    {
        lock (sync)
        {
            requestsInFlight--;
            if (requestsInFlight == 0)
            {
                drained?.TrySetResult();
            }
        }
    }

    private void Open()
    {
        TaskCompletionSource? waiting;
        lock (sync)
        {
            waiting = reopened;
            reopened = null;
            drained = null;
        }

        waiting?.SetResult();
    }

    private sealed class Release(Action release) : IDisposable
    {
        private int isReleased;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref isReleased, 1) == 0)
            {
                release();
            }
        }
    }
}
