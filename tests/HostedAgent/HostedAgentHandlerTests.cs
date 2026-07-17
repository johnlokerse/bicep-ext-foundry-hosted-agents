using System.Net;
using FoundryExtension.Client;
using FoundryExtension.HostedAgent;
using FoundryExtension.Tests.Helpers;

namespace FoundryExtension.Tests.HostedAgent;

[TestClass]
public class HostedAgentHandlerTests
{
    private RecordingHttpMessageHandler http = null!;
    private TestHostedAgentHandler handler = null!;

    [TestInitialize]
    public void Initialize()
    {
        http = new RecordingHttpMessageHandler();
        handler = new TestHostedAgentHandler(
            new TestFoundryClientFactory(http, new FakeTokenCredential()));
    }

    [TestMethod]
    public async Task Create_WhenAgentDoesNotExist_CreatesVersionOne()
    {
        http.Enqueue(HttpStatusCode.NotFound);
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.AgentJson(status: "creating"));
        http.EnqueueJson(HttpStatusCode.OK, TestData.VersionJson());
        var request = CreateRequest();

        var response = await handler.ExecuteCreateOrUpdate(request);

        response.Properties!.Version.Should().Be("1");
        response.Properties.Status.Should().Be("active");
        response.Properties.DeploymentAction.Should().Be("create");
        response.Properties.PrincipalId.Should().Be("principal-id");
        http.Requests.Select(item => item.Method)
            .Should().Equal(HttpMethod.Get, HttpMethod.Post, HttpMethod.Get);
    }

    [TestMethod]
    public async Task Create_WhenDefinitionMatches_DoesNotCreateVersion()
    {
        http.EnqueueJson(HttpStatusCode.OK, TestData.AgentJson());
        http.EnqueueJson(HttpStatusCode.OK, TestData.VersionJson());
        var request = CreateRequest();

        var response = await handler.ExecuteCreateOrUpdate(request);

        response.Properties!.Version.Should().Be("1");
        response.Properties.DeploymentAction.Should().Be("none");
        http.Requests.Select(item => item.Method)
            .Should().Equal(HttpMethod.Get, HttpMethod.Get);
    }

    [TestMethod]
    public async Task Create_WhenDefinitionChanges_CreatesOneNewVersion()
    {
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.AgentJson(image: "registry.azurecr.io/agents/echo:1"));
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.VersionJson(image: "registry.azurecr.io/agents/echo:1"));
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.VersionJson(
                version: "2",
                image: "registry.azurecr.io/agents/echo:2"));
        var request = CreateRequest(
            TestData.Resource("registry.azurecr.io/agents/echo:2"));

        var response = await handler.ExecuteCreateOrUpdate(request);

        response.Properties!.Version.Should().Be("2");
        response.Properties.DeploymentAction.Should().Be("newVersion");
        http.Requests.Should().HaveCount(3);
        http.Requests[2].Uri.AbsolutePath.Should().EndWith(
            "/agents/echo-agent/versions");
    }

    [TestMethod]
    public async Task Create_WhenMatchingLatestVersionFailed_RetriesAsNewVersion()
    {
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.AgentJson());
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.VersionJson(status: "failed"));
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.VersionJson(version: "2"));
        var request = CreateRequest();

        var response = await handler.ExecuteCreateOrUpdate(request);

        response.Properties!.Version.Should().Be("2");
        response.Properties.DeploymentAction.Should().Be("retryVersion");
    }

    [TestMethod]
    public async Task Preview_WhenDefinitionChanges_DoesNotMutateRemoteState()
    {
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.AgentJson(image: "registry.azurecr.io/agents/echo:1"));
        http.EnqueueJson(
            HttpStatusCode.OK,
            TestData.VersionJson(image: "registry.azurecr.io/agents/echo:1"));
        var request = CreateRequest(
            TestData.Resource("registry.azurecr.io/agents/echo:2"));

        var response = await handler.ExecutePreview(request);

        response.Properties!.DeploymentAction.Should().Be("newVersion");
        http.Requests.Select(item => item.Method)
            .Should().Equal(HttpMethod.Get, HttpMethod.Get);
    }

    [TestMethod]
    public async Task Delete_DeletesTheWholeLogicalAgent()
    {
        http.Enqueue(HttpStatusCode.NoContent);
        var request = new TestHostedAgentHandler.ReferenceRequest
        {
            Type = "HostedAgent",
            Config = new Configuration
            {
                ProjectEndpoint = TestData.ProjectEndpoint,
            },
            Identifiers = new HostedAgentIdentifiers
            {
                Name = "echo-agent",
            },
        };

        var response = await handler.ExecuteDelete(request);

        response.Properties.Should().BeNull();
        http.Requests.Should().ContainSingle();
        http.Requests[0].Method.Should().Be(HttpMethod.Delete);
        http.Requests[0].Uri.AbsolutePath.Should().EndWith("/agents/echo-agent");
    }

    private static TestHostedAgentHandler.ResourceRequest CreateRequest(
        HostedAgentResource? resource = null) => new()
        {
            Type = "HostedAgent",
            Config = new Configuration
            {
                ProjectEndpoint = TestData.ProjectEndpoint,
            },
            Properties = resource ?? TestData.Resource(),
        };

    private sealed class TestHostedAgentHandler(IFoundryClientFactory factory)
        : HostedAgentHandler(factory)
    {
        public Task<ResourceResponse> ExecuteCreateOrUpdate(ResourceRequest request) =>
            base.CreateOrUpdate(request, CancellationToken.None);

        public Task<ResourceResponse> ExecutePreview(ResourceRequest request) =>
            base.Preview(request, CancellationToken.None);

        public Task<ResourceResponse> ExecuteDelete(ReferenceRequest request) =>
            base.Delete(request, CancellationToken.None);
    }
}
