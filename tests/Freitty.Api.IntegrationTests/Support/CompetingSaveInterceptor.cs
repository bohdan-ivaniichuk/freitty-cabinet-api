using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Freitty.Api.IntegrationTests.Support;

// Makes a lost race deterministic: once armed, the next save waits while another writer changes the same order and
// commits, then continues with the version it read. The action is taken before it runs, so the competing save —
// which passes through this interceptor too — finds it disarmed.
internal sealed class CompetingSaveInterceptor : SaveChangesInterceptor
{
    private Func<Task>? competingWrite;

    public void Arm(Func<Task> write)
    {
        competingWrite = write;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Func<Task>? write = Interlocked.Exchange(ref competingWrite, null);
        if (write is not null)
        {
            await write();
        }

        return result;
    }
}
