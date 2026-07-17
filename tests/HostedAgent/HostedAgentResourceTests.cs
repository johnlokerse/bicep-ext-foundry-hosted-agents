using Bicep.Local.Extension.Types.Attributes;
using FoundryExtension.HostedAgent;

namespace FoundryExtension.Tests.HostedAgent;

[TestClass]
public class HostedAgentResourceTests
{
    [TestMethod]
    public void Defaults_AreSafeForHostedAgents()
    {
        var resource = new HostedAgentResource
        {
            Name = "agent",
            Image = "registry.azurecr.io/agents/test:1",
            Protocols =
            [
                new HostedAgentProtocol
                {
                    Protocol = "responses",
                    Version = "2.0.0",
                },
            ],
        };

        resource.Cpu.Should().Be("1");
        resource.Memory.Should().Be("2Gi");
        resource.EnvironmentVariables.Should().BeEmpty();
        resource.RaiPolicy.Should().BeNull();
    }

    [TestMethod]
    public void ResourceType_IsHostedAgent()
    {
        var attribute = typeof(HostedAgentResource)
            .GetCustomAttributes(typeof(ResourceTypeAttribute), inherit: false)
            .Cast<ResourceTypeAttribute>()
            .Single();

        attribute.Name.Should().Be("HostedAgent");
        attribute.ApiVersion.Should().BeNull();
    }
}

