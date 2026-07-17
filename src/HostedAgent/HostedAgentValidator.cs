using System.Text.RegularExpressions;

namespace FoundryExtension.HostedAgent;

internal static partial class HostedAgentValidator
{
    private static readonly HashSet<string> SupportedProtocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "responses",
        "invocations",
        "invocations_ws",
    };

    public static void Validate(HostedAgentResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(resource.Name) || !AgentNameRegex().IsMatch(resource.Name))
        {
            errors.Add("name must start and end with an alphanumeric character, contain only alphanumeric characters or hyphens, and be at most 63 characters.");
        }

        if (string.IsNullOrWhiteSpace(resource.Image) ||
            resource.Image.Any(char.IsWhiteSpace) ||
            !resource.Image.Contains('/', StringComparison.Ordinal))
        {
            errors.Add("image must be a full container image reference such as registry.azurecr.io/repository:tag.");
        }

        if (string.IsNullOrWhiteSpace(resource.Cpu))
        {
            errors.Add("cpu is required.");
        }

        if (string.IsNullOrWhiteSpace(resource.Memory) || !MemoryRegex().IsMatch(resource.Memory))
        {
            errors.Add("memory must use a Mi or Gi suffix, for example 2Gi.");
        }

        if (resource.Protocols is null || resource.Protocols.Count == 0)
        {
            errors.Add("at least one protocol is required.");
        }
        else
        {
            var protocols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var protocol in resource.Protocols)
            {
                if (protocol is null || !SupportedProtocols.Contains(protocol.Protocol))
                {
                    errors.Add("protocol must be responses, invocations, or invocations_ws.");
                    continue;
                }

                if (!protocols.Add(protocol.Protocol))
                {
                    errors.Add($"protocol '{protocol.Protocol}' is declared more than once.");
                }

                if (string.IsNullOrWhiteSpace(protocol.Version))
                {
                    errors.Add($"protocol '{protocol.Protocol}' must specify a version.");
                }
            }
        }

        foreach (var variable in resource.EnvironmentVariables ?? [])
        {
            if (string.IsNullOrWhiteSpace(variable.Key))
            {
                errors.Add("environment variable names may not be empty.");
            }
            else if (variable.Key.StartsWith("FOUNDRY_", StringComparison.OrdinalIgnoreCase) ||
                     variable.Key.Equals("APPLICATIONINSIGHTS_CONNECTION_STRING", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"environment variable '{variable.Key}' is reserved and injected by Foundry.");
            }
        }

        if (resource.RaiPolicy?.ResourceId is { } raiPolicyId &&
            (string.IsNullOrWhiteSpace(raiPolicyId) || !RaiPolicyResourceIdRegex().IsMatch(raiPolicyId)))
        {
            errors.Add("raiPolicy.resourceId must be the full ARM resource ID of a Microsoft.CognitiveServices/accounts/raiPolicies resource.");
        }

        if (errors.Count > 0)
        {
            throw new HostedAgentValidationException(errors);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex AgentNameRegex();

    [GeneratedRegex("^[1-9][0-9]*(?:Mi|Gi)$", RegexOptions.CultureInvariant)]
    private static partial Regex MemoryRegex();

    [GeneratedRegex(
        "^/subscriptions/[^/]+/resourceGroups/[^/]+/providers/Microsoft\\.CognitiveServices/accounts/[^/]+/raiPolicies/[^/]+/?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RaiPolicyResourceIdRegex();
}

internal sealed class HostedAgentValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

