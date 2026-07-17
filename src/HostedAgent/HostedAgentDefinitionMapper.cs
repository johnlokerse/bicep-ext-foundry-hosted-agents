using FoundryExtension.Client;

namespace FoundryExtension.HostedAgent;

internal static class HostedAgentDefinitionMapper
{
    public static HostedAgentDefinitionPayload ToPayload(HostedAgentResource resource) => new()
    {
        ContainerConfiguration = new ContainerConfigurationPayload
        {
            Image = resource.Image,
        },
        Cpu = resource.Cpu,
        Memory = resource.Memory,
        ProtocolVersions = resource.Protocols
            .Select(protocol => new ProtocolVersionPayload
            {
                Protocol = protocol.Protocol.ToLowerInvariant(),
                Version = protocol.Version,
            })
            .ToList(),
        EnvironmentVariables = new Dictionary<string, string>(
            resource.EnvironmentVariables ?? [],
            StringComparer.Ordinal),
        RaiConfig = resource.RaiPolicy is null
            ? null
            : new RaiConfigurationPayload
            {
                RaiPolicyName = resource.RaiPolicy.ResourceId,
            },
    };

    public static HostedAgentResource FromResponse(AgentVersionResponse response)
    {
        var resource = new HostedAgentResource
        {
            Name = response.Name,
            Image = response.Definition.ContainerConfiguration.Image,
            Cpu = response.Definition.Cpu,
            Memory = response.Definition.Memory,
            Protocols = response.Definition.ProtocolVersions
                .Select(protocol => new HostedAgentProtocol
                {
                    Protocol = protocol.Protocol,
                    Version = protocol.Version,
                })
                .ToList(),
            EnvironmentVariables = new Dictionary<string, string>(
                response.Definition.EnvironmentVariables ?? [],
                StringComparer.Ordinal),
            RaiPolicy = response.Definition.RaiConfig is null
                ? null
                : new HostedAgentRaiPolicy
                {
                    ResourceId = IsDefaultRaiPolicy(response.Definition.RaiConfig.RaiPolicyName)
                        ? null
                        : response.Definition.RaiConfig.RaiPolicyName,
                },
        };

        ApplyOutputs(resource, response, "none", projectEndpointClient: null);
        return resource;
    }

    public static void ApplyOutputs(
        HostedAgentResource resource,
        AgentVersionResponse response,
        string action,
        FoundryRestClient? projectEndpointClient)
    {
        resource.Version = response.Version;
        resource.Status = response.Status;
        resource.PrincipalId = response.InstanceIdentity?.PrincipalId;
        resource.ClientId = response.InstanceIdentity?.ClientId;
        resource.DeploymentAction = action;
        resource.Endpoints = projectEndpointClient?.GetProtocolEndpoints(
            response.Name,
            response.Definition.ProtocolVersions);
    }

    internal static bool IsDefaultRaiPolicy(string? policyName) =>
        string.IsNullOrWhiteSpace(policyName) ||
        string.Equals(policyName, "Microsoft.DefaultV2", StringComparison.OrdinalIgnoreCase);
}

