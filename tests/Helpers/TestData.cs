using System.Text.Json;
using FoundryExtension.Client;
using FoundryExtension.HostedAgent;

namespace FoundryExtension.Tests.Helpers;

internal static class TestData
{
    public const string ProjectEndpoint =
        "https://account.services.ai.azure.com/api/projects/test-project";

    public const string RaiPolicyId =
        "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/account/raiPolicies/strict";

    public static HostedAgentResource Resource(
        string image = "registry.azurecr.io/agents/echo:1",
        HostedAgentRaiPolicy? raiPolicy = null) => new()
        {
            Name = "echo-agent",
            Image = image,
            Cpu = "1",
            Memory = "2Gi",
            Protocols =
        [
            new HostedAgentProtocol
            {
                Protocol = "responses",
                Version = "2.0.0",
            },
        ],
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MODEL_DEPLOYMENT_NAME"] = "gpt-5-mini",
            },
            RaiPolicy = raiPolicy,
        };

    public static HostedAgentDefinitionPayload Definition(
        string image = "registry.azurecr.io/agents/echo:1",
        RaiConfigurationPayload? raiConfig = null) => new()
        {
            ContainerConfiguration = new ContainerConfigurationPayload
            {
                Image = image,
            },
            Cpu = "1",
            Memory = "2Gi",
            ProtocolVersions =
        [
            new ProtocolVersionPayload
            {
                Protocol = "responses",
                Version = "2.0.0",
            },
        ],
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MODEL_DEPLOYMENT_NAME"] = "gpt-5-mini",
            },
            RaiConfig = raiConfig,
        };

    public static AgentVersionResponse Version(
        string version = "1",
        string status = "active",
        string image = "registry.azurecr.io/agents/echo:1",
        RaiConfigurationPayload? raiConfig = null,
        FoundryError? error = null) => new()
        {
            Name = "echo-agent",
            Version = version,
            Status = status,
            Definition = Definition(image, raiConfig),
            InstanceIdentity = new AgentIdentityResponse
            {
                PrincipalId = "principal-id",
                ClientId = "client-id",
            },
            Error = error,
        };

    public static string VersionJson(
        string version = "1",
        string status = "active",
        string image = "registry.azurecr.io/agents/echo:1",
        object? raiConfig = null,
        object? error = null)
    {
        var definition = new Dictionary<string, object?>
        {
            ["kind"] = "hosted",
            ["container_configuration"] = new { image },
            ["cpu"] = "1",
            ["memory"] = "2Gi",
            ["protocol_versions"] = new[]
            {
                new
                {
                    protocol = "responses",
                    version = "2.0.0",
                },
            },
            ["environment_variables"] = new Dictionary<string, string>
            {
                ["MODEL_DEPLOYMENT_NAME"] = "gpt-5-mini",
            },
        };

        if (raiConfig is not null)
        {
            definition["rai_config"] = raiConfig;
        }

        return JsonSerializer.Serialize(new
        {
            name = "echo-agent",
            version,
            status,
            definition,
            instance_identity = new
            {
                principal_id = "principal-id",
                client_id = "client-id",
            },
            error,
        });
    }

    public static string AgentJson(
        string version = "1",
        string status = "active",
        string image = "registry.azurecr.io/agents/echo:1",
        object? raiConfig = null,
        object? error = null)
    {
        var latest = JsonSerializer.Deserialize<JsonElement>(
            VersionJson(version, status, image, raiConfig, error));

        return JsonSerializer.Serialize(new
        {
            @object = "agent",
            name = "echo-agent",
            state = "enabled",
            versions = new
            {
                latest,
            },
        });
    }
}
