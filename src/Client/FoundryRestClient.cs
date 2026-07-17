using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;

namespace FoundryExtension.Client;

public sealed class FoundryRestClient : IDisposable
{
    internal const string ApiVersion = "v1";
    internal const string TokenScope = "https://ai.azure.com/.default";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient httpClient;
    private readonly TokenCredential credential;
    private readonly Uri projectEndpoint;
    private readonly FoundryClientOptions options;

    internal FoundryRestClient(
        HttpClient httpClient,
        TokenCredential credential,
        string projectEndpoint,
        FoundryClientOptions options)
    {
        this.httpClient = httpClient;
        this.credential = credential;
        this.projectEndpoint = NormalizeProjectEndpoint(projectEndpoint);
        this.options = options;
    }

    internal async Task<AgentVersionResponse?> GetAgentAsync(
        string agentName,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"agents/{Escape(agentName)}?api-version={ApiVersion}",
            content: null,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var agent = await ReadRequiredAsync<AgentResponse>(response, cancellationToken);
        return await GetAgentVersionAsync(
            agentName,
            agent.Versions.Latest.Version,
            cancellationToken);
    }

    internal async Task<AgentVersionResponse> GetAgentVersionAsync(
        string agentName,
        string version,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"agents/{Escape(agentName)}/versions/{Escape(version)}?api-version={ApiVersion}",
            content: null,
            cancellationToken);

        return await ReadRequiredAsync<AgentVersionResponse>(response, cancellationToken);
    }

    internal async Task<AgentVersionResponse> CreateAgentAsync(
        string agentName,
        HostedAgentDefinitionPayload definition,
        CancellationToken cancellationToken)
    {
        var request = new CreateAgentRequest
        {
            Name = agentName,
            Definition = definition,
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            $"agents?api-version={ApiVersion}",
            JsonContent.Create(request, options: JsonOptions),
            cancellationToken);

        var agent = await ReadRequiredAsync<AgentResponse>(response, cancellationToken);
        return agent.Versions.Latest;
    }

    internal async Task<AgentVersionResponse> CreateAgentVersionAsync(
        string agentName,
        HostedAgentDefinitionPayload definition,
        CancellationToken cancellationToken)
    {
        var request = new CreateAgentVersionRequest
        {
            Definition = definition,
        };

        using var response = await SendAsync(
            HttpMethod.Post,
            $"agents/{Escape(agentName)}/versions?api-version={ApiVersion}",
            JsonContent.Create(request, options: JsonOptions),
            cancellationToken);

        return await ReadRequiredAsync<AgentVersionResponse>(response, cancellationToken);
    }

    internal async Task DeleteAgentAsync(string agentName, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Delete,
            $"agents/{Escape(agentName)}?api-version={ApiVersion}",
            content: null,
            cancellationToken);

        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    internal async Task<AgentVersionResponse> WaitUntilActiveAsync(
        AgentVersionResponse version,
        CancellationToken cancellationToken)
    {
        if (IsActive(version))
        {
            return version;
        }

        ThrowIfFailed(version);

        var started = Stopwatch.GetTimestamp();
        while (Stopwatch.GetElapsedTime(started) < options.ProvisioningTimeout)
        {
            await Task.Delay(options.PollInterval, cancellationToken);
            version = await GetAgentVersionAsync(version.Name, version.Version, cancellationToken);

            if (IsActive(version))
            {
                return version;
            }

            ThrowIfFailed(version);

            if (!string.Equals(version.Status, "creating", StringComparison.OrdinalIgnoreCase))
            {
                throw new AgentProvisioningException(
                    "UnexpectedStatus",
                    $"Agent '{version.Name}' version '{version.Version}' entered unexpected status '{version.Status ?? "<null>"}'.");
            }
        }

        throw new TimeoutException(
            $"Agent '{version.Name}' version '{version.Version}' did not become active within {options.ProvisioningTimeout}.");
    }

    internal Dictionary<string, string> GetProtocolEndpoints(
        string agentName,
        IEnumerable<ProtocolVersionPayload> protocols)
    {
        var endpoints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var protocol in protocols)
        {
            var path = protocol.Protocol.ToLowerInvariant() switch
            {
                "responses" => "openai/responses",
                "invocations" => "invocations",
                "invocations_ws" => "invocations_ws",
                _ => protocol.Protocol,
            };

            endpoints[protocol.Protocol] =
                new Uri(
                    projectEndpoint,
                    $"agents/{Escape(agentName)}/endpoint/protocols/{path}?api-version={ApiVersion}")
                .AbsoluteUri;
        }

        return endpoints;
    }

    public void Dispose() => httpClient.Dispose();

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string relativeUri,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext([TokenScope]),
            cancellationToken);

        using var request = new HttpRequestMessage(method, new Uri(projectEndpoint, relativeUri))
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
    }

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw new FoundryApiException(
                    response.StatusCode,
                    "InvalidResponse",
                    "Microsoft Foundry returned an empty JSON response.");
        }
        catch (JsonException exception)
        {
            throw new FoundryApiException(
                response.StatusCode,
                "InvalidResponse",
                $"Microsoft Foundry returned an invalid JSON response. {exception.Message}");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        ErrorEnvelope? envelope = null;
        try
        {
            envelope = JsonSerializer.Deserialize<ErrorEnvelope>(body, JsonOptions);
        }
        catch (JsonException)
        {
        }

        var code = envelope?.Error?.Code;
        var detail = envelope?.Error?.Message;
        if (string.IsNullOrWhiteSpace(detail))
        {
            detail = string.IsNullOrWhiteSpace(body)
                ? response.ReasonPhrase ?? "The service returned an error."
                : body[..Math.Min(body.Length, 2_000)];
        }

        throw new FoundryApiException(
            response.StatusCode,
            code,
            $"Microsoft Foundry request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase})." +
            (string.IsNullOrWhiteSpace(code) ? $" {detail}" : $" {code}: {detail}"));
    }

    private static bool IsActive(AgentVersionResponse version) =>
        string.Equals(version.Status, "active", StringComparison.OrdinalIgnoreCase);

    private static void ThrowIfFailed(AgentVersionResponse version)
    {
        if (!string.Equals(version.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new AgentProvisioningException(
            version.Error?.Code,
            $"Agent '{version.Name}' version '{version.Version}' failed to provision." +
            (string.IsNullOrWhiteSpace(version.Error?.Message) ? string.Empty : $" {version.Error.Message}"));
    }

    private static Uri NormalizeProjectEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !uri.AbsolutePath.Contains("/api/projects/", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "projectEndpoint must be an HTTPS Microsoft Foundry project endpoint such as https://account.services.ai.azure.com/api/projects/project.",
                nameof(endpoint));
        }

        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
}
