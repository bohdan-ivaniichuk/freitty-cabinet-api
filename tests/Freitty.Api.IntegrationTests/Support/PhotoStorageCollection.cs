namespace Freitty.Api.IntegrationTests.Support;

// Every API of the run stores photos in the same container, and a rebuild of the sample data deletes every upload in
// it. The tests that rebuild and the tests that read back what they uploaded therefore take turns.
[CollectionDefinition(Name)]
public sealed class PhotoStorageCollection
{
    public const string Name = "Photo storage";
}
