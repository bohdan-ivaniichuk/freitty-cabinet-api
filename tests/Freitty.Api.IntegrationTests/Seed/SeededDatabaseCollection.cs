namespace Freitty.Api.IntegrationTests.Seed;

// The read-only seed tests share one seeded database instead of seeding one per test class.
[CollectionDefinition(Name)]
public sealed class SeededDatabaseCollection : ICollectionFixture<SeededDatabase>
{
    public const string Name = "Seeded database";
}
