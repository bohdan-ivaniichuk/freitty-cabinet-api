using Microsoft.EntityFrameworkCore;

namespace Freitty.Api.IntegrationTests.Persistence;

public sealed class MigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task EmptyDatabaseIsMigratedWhenApiStarts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string connectionString = await postgres.CreateDatabaseAsync(cancellationToken);
        await using var database = new TestDatabase(connectionString);

        (string[] Known, string[] Applied) migrations = await database.InContextAsync(async db =>
            (db.Database.GetMigrations().ToArray(),
                (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToArray()));

        Assert.NotEmpty(migrations.Known);
        Assert.Equal(migrations.Known, migrations.Applied);
    }

    [Fact]
    public async Task ModelHasNoChangesMissingFromMigrations()
    {
        await using var database = new TestDatabase(postgres.ConnectionString);

        bool hasPendingChanges = await database.InContextAsync(
            db => Task.FromResult(db.Database.HasPendingModelChanges()));

        Assert.False(hasPendingChanges);
    }
}
