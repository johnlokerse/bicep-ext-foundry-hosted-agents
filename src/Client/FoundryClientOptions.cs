namespace FoundryExtension.Client;

internal sealed class FoundryClientOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    public TimeSpan ProvisioningTimeout { get; init; } = TimeSpan.FromMinutes(10);
}

