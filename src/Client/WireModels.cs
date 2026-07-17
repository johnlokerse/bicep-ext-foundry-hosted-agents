using System.Text.Json.Serialization;

namespace FoundryExtension.Client;

internal sealed class CreateAgentRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("definition")]
    public required HostedAgentDefinitionPayload Definition { get; init; }
}

internal sealed class CreateAgentVersionRequest
{
    [JsonPropertyName("definition")]
    public required HostedAgentDefinitionPayload Definition { get; init; }
}

internal sealed class HostedAgentDefinitionPayload
{
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "hosted";

    [JsonPropertyName("container_configuration")]
    public required ContainerConfigurationPayload ContainerConfiguration { get; init; }

    [JsonPropertyName("cpu")]
    public required string Cpu { get; init; }

    [JsonPropertyName("memory")]
    public required string Memory { get; init; }

    [JsonPropertyName("protocol_versions")]
    public required List<ProtocolVersionPayload> ProtocolVersions { get; init; }

    [JsonPropertyName("environment_variables")]
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new(StringComparer.Ordinal);

    [JsonPropertyName("rai_config")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RaiConfigurationPayload? RaiConfig { get; init; }
}

internal sealed class ContainerConfigurationPayload
{
    [JsonPropertyName("image")]
    public required string Image { get; init; }
}

internal sealed class ProtocolVersionPayload
{
    [JsonPropertyName("protocol")]
    public required string Protocol { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

internal sealed class RaiConfigurationPayload
{
    [JsonPropertyName("rai_policy_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RaiPolicyName { get; init; }
}

internal sealed class AgentResponse
{
    [JsonPropertyName("versions")]
    public required AgentVersionsResponse Versions { get; init; }
}

internal sealed class AgentVersionsResponse
{
    [JsonPropertyName("latest")]
    public required AgentVersionResponse Latest { get; init; }
}

internal sealed class AgentVersionResponse
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("definition")]
    public required HostedAgentDefinitionPayload Definition { get; init; }

    [JsonPropertyName("instance_identity")]
    public AgentIdentityResponse? InstanceIdentity { get; init; }

    [JsonPropertyName("error")]
    public FoundryError? Error { get; init; }
}

internal sealed class AgentIdentityResponse
{
    [JsonPropertyName("principal_id")]
    public string? PrincipalId { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }
}

internal sealed class ErrorEnvelope
{
    [JsonPropertyName("error")]
    public FoundryError? Error { get; init; }
}

internal sealed class FoundryError
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
