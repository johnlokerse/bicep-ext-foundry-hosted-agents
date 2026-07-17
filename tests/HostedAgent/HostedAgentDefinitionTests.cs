using System.Text.Json;
using FoundryExtension.Client;
using FoundryExtension.HostedAgent;
using FoundryExtension.Tests.Helpers;

namespace FoundryExtension.Tests.HostedAgent;

[TestClass]
public class HostedAgentDefinitionTests
{
    [TestMethod]
    public void NoRaiPolicy_OmitsRaiConfig()
    {
        var payload = HostedAgentDefinitionMapper.ToPayload(TestData.Resource());

        var json = JsonSerializer.Serialize(payload);

        json.Should().NotContain("rai_config");
    }

    [TestMethod]
    public void EmptyRaiPolicy_WritesEmptyRaiConfig()
    {
        var payload = HostedAgentDefinitionMapper.ToPayload(
            TestData.Resource(raiPolicy: new HostedAgentRaiPolicy()));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));

        var raiConfig = json.RootElement.GetProperty("rai_config");
        raiConfig.ValueKind.Should().Be(JsonValueKind.Object);
        raiConfig.EnumerateObject().Should().BeEmpty();
    }

    [TestMethod]
    public void CustomRaiPolicy_WritesFullResourceId()
    {
        var payload = HostedAgentDefinitionMapper.ToPayload(
            TestData.Resource(raiPolicy: new HostedAgentRaiPolicy
            {
                ResourceId = TestData.RaiPolicyId,
            }));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(payload));

        json.RootElement
            .GetProperty("rai_config")
            .GetProperty("rai_policy_name")
            .GetString()
            .Should().Be(TestData.RaiPolicyId);
    }

    [TestMethod]
    public void Comparison_IgnoresDictionaryAndProtocolOrder()
    {
        var left = TestData.Definition();
        left.EnvironmentVariables["SECOND"] = "two";
        left.ProtocolVersions.Add(new ProtocolVersionPayload
        {
            Protocol = "invocations",
            Version = "1.0.0",
        });

        var right = TestData.Definition();
        right.ProtocolVersions.Insert(0, new ProtocolVersionPayload
        {
            Protocol = "INVOCATIONS",
            Version = "1.0.0",
        });
        right.EnvironmentVariables.Clear();
        right.EnvironmentVariables["SECOND"] = "two";
        right.EnvironmentVariables["MODEL_DEPLOYMENT_NAME"] = "gpt-5-mini";

        HostedAgentDefinitionComparer.Equals(left, right).Should().BeTrue();
    }

    [TestMethod]
    public void Comparison_TreatsExplicitDefaultPoliciesAsEquivalent()
    {
        var requestedDefault = TestData.Definition(
            raiConfig: new RaiConfigurationPayload());
        var resolvedDefault = TestData.Definition(
            raiConfig: new RaiConfigurationPayload
            {
                RaiPolicyName = "Microsoft.DefaultV2",
            });

        HostedAgentDefinitionComparer.Equals(requestedDefault, resolvedDefault)
            .Should().BeTrue();
    }

    [TestMethod]
    public void Comparison_DistinguishesNoGuardrailFromDefaultGuardrail()
    {
        var noGuardrail = TestData.Definition();
        var defaultGuardrail = TestData.Definition(
            raiConfig: new RaiConfigurationPayload());

        HostedAgentDefinitionComparer.Equals(noGuardrail, defaultGuardrail)
            .Should().BeFalse();
    }

    [TestMethod]
    public void Comparison_DetectsImageChange()
    {
        HostedAgentDefinitionComparer.Equals(
                TestData.Definition("registry.azurecr.io/agents/echo:1"),
                TestData.Definition("registry.azurecr.io/agents/echo:2"))
            .Should().BeFalse();
    }
}
