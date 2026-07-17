using FoundryExtension.Client;

namespace FoundryExtension.Tests.Helpers;

internal sealed class TestFoundryClientFactory(
    RecordingHttpMessageHandler handler,
    FakeTokenCredential credential,
    FoundryClientOptions? options = null) : IFoundryClientFactory
{
    private readonly FoundryClientOptions options = options ?? new FoundryClientOptions
    {
        PollInterval = TimeSpan.Zero,
        ProvisioningTimeout = TimeSpan.FromSeconds(1),
    };

    public FoundryRestClient Create(string projectEndpoint) =>
        new(
            new HttpClient(handler, disposeHandler: false),
            credential,
            projectEndpoint,
            options);
}

