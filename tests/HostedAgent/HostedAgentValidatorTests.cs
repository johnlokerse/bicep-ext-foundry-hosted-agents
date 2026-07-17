using FoundryExtension.HostedAgent;
using FoundryExtension.Tests.Helpers;

namespace FoundryExtension.Tests.HostedAgent;

[TestClass]
public class HostedAgentValidatorTests
{
    [TestMethod]
    public void ValidResource_PassesValidation()
    {
        var action = () => HostedAgentValidator.Validate(TestData.Resource());

        action.Should().NotThrow();
    }

    [TestMethod]
    public void OptionalRaiPolicy_PassesValidation()
    {
        var withoutGuardrail = TestData.Resource();
        var defaultGuardrail = TestData.Resource(raiPolicy: new HostedAgentRaiPolicy());
        var customGuardrail = TestData.Resource(raiPolicy: new HostedAgentRaiPolicy
        {
            ResourceId = TestData.RaiPolicyId,
        });

        Action withoutAction = () => HostedAgentValidator.Validate(withoutGuardrail);
        Action defaultAction = () => HostedAgentValidator.Validate(defaultGuardrail);
        Action customAction = () => HostedAgentValidator.Validate(customGuardrail);

        withoutAction.Should().NotThrow();
        defaultAction.Should().NotThrow();
        customAction.Should().NotThrow();
    }

    [TestMethod]
    [DataRow("-agent")]
    [DataRow("agent-")]
    [DataRow("agent_name")]
    [DataRow("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void InvalidName_IsRejected(string name)
    {
        var resource = TestData.Resource();
        resource.Name = name;

        var action = () => HostedAgentValidator.Validate(resource);

        action
            .Should().Throw<HostedAgentValidationException>()
            .WithMessage("*name must*");
    }

    [TestMethod]
    public void DuplicateProtocol_IsRejected()
    {
        var resource = TestData.Resource();
        resource.Protocols.Add(new HostedAgentProtocol
        {
            Protocol = "Responses",
            Version = "2.0.0",
        });

        var action = () => HostedAgentValidator.Validate(resource);

        action
            .Should().Throw<HostedAgentValidationException>()
            .WithMessage("*declared more than once*");
    }

    [TestMethod]
    [DataRow("FOUNDRY_AGENT_NAME")]
    [DataRow("APPLICATIONINSIGHTS_CONNECTION_STRING")]
    public void PlatformEnvironmentVariable_IsRejected(string variable)
    {
        var resource = TestData.Resource();
        resource.EnvironmentVariables[variable] = "value";

        var action = () => HostedAgentValidator.Validate(resource);

        action
            .Should().Throw<HostedAgentValidationException>()
            .WithMessage("*reserved*");
    }

    [TestMethod]
    [DataRow("strict")]
    [DataRow("/subscriptions/sub/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/account")]
    [DataRow("   ")]
    public void InvalidRaiPolicyResourceId_IsRejected(string resourceId)
    {
        var resource = TestData.Resource(raiPolicy: new HostedAgentRaiPolicy
        {
            ResourceId = resourceId,
        });

        var action = () => HostedAgentValidator.Validate(resource);

        action
            .Should().Throw<HostedAgentValidationException>()
            .WithMessage("*full ARM resource ID*");
    }
}
