using Azure.Core;

namespace FoundryExtension.Client;

public interface IFoundryClientFactory
{
    FoundryRestClient Create(string projectEndpoint);
}

public sealed class FoundryClientFactory(
    IHttpClientFactory httpClientFactory,
    TokenCredential credential) : IFoundryClientFactory
{
    public const string HttpClientName = "MicrosoftFoundry";

    public FoundryRestClient Create(string projectEndpoint) =>
        new(
            httpClientFactory.CreateClient(HttpClientName),
            credential,
            projectEndpoint,
            new FoundryClientOptions());
}

