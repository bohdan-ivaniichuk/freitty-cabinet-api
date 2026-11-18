namespace Freitty.Api.IntegrationTests.Support;

// The body of the request that submits a draft, as a client sends it.
internal sealed record SubmitRequest(int ExpectedVersion);
