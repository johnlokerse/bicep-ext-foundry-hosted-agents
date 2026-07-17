namespace FoundryExtension.HostedAgent;

public class HostedAgentIdentifiers
{
    [TypeProperty(
        "Hosted agent name. It must start and end with an alphanumeric character, may contain hyphens, and may not exceed 63 characters.",
        ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

public sealed class HostedAgentProtocol
{
    [TypeProperty(
        "Hosted agent protocol: responses, invocations, or invocations_ws.",
        ObjectTypePropertyFlags.Required)]
    public required string Protocol { get; set; }

    [TypeProperty("Protocol library version exposed by the container.", ObjectTypePropertyFlags.Required)]
    public required string Version { get; set; }
}

public sealed class HostedAgentRaiPolicy
{
    [TypeProperty(
        "Full ARM resource ID of an existing Microsoft.CognitiveServices/accounts/raiPolicies resource. Omit this property to use Microsoft.DefaultV2.")]
    public string? ResourceId { get; set; }
}

[ResourceType("HostedAgent")]
public sealed class HostedAgentResource : HostedAgentIdentifiers
{
    [TypeProperty("Full Azure Container Registry image reference with an immutable tag or digest.", ObjectTypePropertyFlags.Required)]
    public required string Image { get; set; }

    [TypeProperty("CPU allocation for the hosted container.", ObjectTypePropertyFlags.Required)]
    public string Cpu { get; set; };

    [TypeProperty("Memory allocation for the hosted container, for example 2Gi.", ObjectTypePropertyFlags.Required)]
    public string Memory { get; set; } = "2Gi";

    [TypeProperty("One or more protocols exposed by the container.", ObjectTypePropertyFlags.Required)]
    public required List<HostedAgentProtocol> Protocols { get; set; }

    [TypeProperty("Environment variables injected into the hosted container.")]
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.Ordinal);

    [TypeProperty("Optional Responsible AI policy guardrail. Omit to deploy without a guardrail.")]
    public HostedAgentRaiPolicy? RaiPolicy { get; set; }

    [TypeProperty("[OUTPUT] Latest deployed immutable agent version.", ObjectTypePropertyFlags.ReadOnly)]
    public string? Version { get; set; }

    [TypeProperty("[OUTPUT] Latest agent provisioning status.", ObjectTypePropertyFlags.ReadOnly)]
    public string? Status { get; set; }

    [TypeProperty("[OUTPUT] Protocol endpoint URLs for the hosted agent.", ObjectTypePropertyFlags.ReadOnly)]
    public Dictionary<string, string>? Endpoints { get; set; }

    [TypeProperty("[OUTPUT] Principal ID of the dedicated agent identity.", ObjectTypePropertyFlags.ReadOnly)]
    public string? PrincipalId { get; set; }

    [TypeProperty("[OUTPUT] Client ID of the dedicated agent identity.", ObjectTypePropertyFlags.ReadOnly)]
    public string? ClientId { get; set; }

    [TypeProperty("[OUTPUT] Action selected by the extension: create, newVersion, none, or retryVersion.", ObjectTypePropertyFlags.ReadOnly)]
    public string? DeploymentAction { get; set; }
}
