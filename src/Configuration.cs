namespace FoundryExtension;

public sealed class Configuration
{
    [TypeProperty(
        "Microsoft Foundry project endpoint, for example https://account.services.ai.azure.com/api/projects/project.",
        ObjectTypePropertyFlags.Required)]
    public required string ProjectEndpoint { get; set; }
}

