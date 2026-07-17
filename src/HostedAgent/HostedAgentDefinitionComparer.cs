using FoundryExtension.Client;

namespace FoundryExtension.HostedAgent;

internal static class HostedAgentDefinitionComparer
{
    public static bool Equals(
        HostedAgentDefinitionPayload desired,
        HostedAgentDefinitionPayload actual)
    {
        if (!string.Equals(actual.Kind, "hosted", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(desired.ContainerConfiguration.Image, actual.ContainerConfiguration.Image, StringComparison.Ordinal) ||
            !string.Equals(desired.Cpu, actual.Cpu, StringComparison.Ordinal) ||
            !string.Equals(desired.Memory, actual.Memory, StringComparison.Ordinal))
        {
            return false;
        }

        if (!DictionaryEquals(
                desired.EnvironmentVariables ?? [],
                actual.EnvironmentVariables ?? [],
                StringComparer.Ordinal))
        {
            return false;
        }

        var desiredProtocols = desired.ProtocolVersions.ToDictionary(
            item => item.Protocol,
            item => item.Version,
            StringComparer.OrdinalIgnoreCase);
        var actualProtocols = actual.ProtocolVersions.ToDictionary(
            item => item.Protocol,
            item => item.Version,
            StringComparer.OrdinalIgnoreCase);

        return DictionaryEquals(desiredProtocols, actualProtocols, StringComparer.OrdinalIgnoreCase) &&
               RaiConfigurationsEqual(desired.RaiConfig, actual.RaiConfig);
    }

    private static bool RaiConfigurationsEqual(
        RaiConfigurationPayload? desired,
        RaiConfigurationPayload? actual)
    {
        if (desired is null || actual is null)
        {
            return desired is null && actual is null;
        }

        var desiredDefault = HostedAgentDefinitionMapper.IsDefaultRaiPolicy(desired.RaiPolicyName);
        var actualDefault = HostedAgentDefinitionMapper.IsDefaultRaiPolicy(actual.RaiPolicyName);
        if (desiredDefault || actualDefault)
        {
            return desiredDefault && actualDefault;
        }

        return string.Equals(
            desired.RaiPolicyName?.TrimEnd('/'),
            actual.RaiPolicyName?.TrimEnd('/'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool DictionaryEquals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right,
        StringComparer keyComparer)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var normalizedRight = new Dictionary<string, string>(right, keyComparer);
        return left.All(item =>
            normalizedRight.TryGetValue(item.Key, out var rightValue) &&
            string.Equals(item.Value, rightValue, StringComparison.Ordinal));
    }
}

