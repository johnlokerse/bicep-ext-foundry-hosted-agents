using System.Net;
using System.Text.Json;
using FoundryExtension.Client;
using FoundryExtension.Tests.Helpers;

namespace FoundryExtension.Tests.Client;

[TestClass]
public class FoundryRestClientTests
{
    private RecordingHttpMessageHandler handler = null!;
    private FakeTokenCredential credential = null!;

    [TestInitialize]
    public void Initialize()
    {
        handler = new RecordingHttpMessageHandler();
        credential = new FakeTokenCredential();
    }

    [TestMethod]
    public async Task GetAgent_ResolvesLatestThroughVersionEndpoint()
    {
        handler.EnqueueJson(HttpStatusCode.OK, TestData.AgentJson());
        handler.EnqueueJson(HttpStatusCode.OK, TestData.VersionJson(status: "failed"));
        using var client = CreateClient();

        var result = await client.GetAgentAsync("echo-agent", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Version.Should().Be("1");
        result.Status.Should().Be("failed");
        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            $"{TestData.ProjectEndpoint}/agents/echo-agent?api-version=v1");
        handler.Requests[1].Uri.AbsoluteUri.Should().Be(
            $"{TestData.ProjectEndpoint}/agents/echo-agent/versions/1?api-version=v1");
        handler.Requests.Should().OnlyContain(request =>
            request.Authorization != null &&
            request.Authorization.Scheme == "Bearer" &&
            request.Authorization.Parameter == "test-token");
        credential.RequestedScopes.Should().HaveCount(2);
        credential.RequestedScopes.SelectMany(scopes => scopes)
            .Should().Equal(
                FoundryRestClient.TokenScope,
                FoundryRestClient.TokenScope);
    }

    [TestMethod]
    public async Task GetAgent_RejectsResponseWithoutLatestVersion()
    {
        handler.EnqueueJson(HttpStatusCode.OK, """{"object":"agent","versions":{}}""");
        using var client = CreateClient();

        var action = () => client.GetAgentAsync("echo-agent", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<FoundryApiException>();
        exception.Which.ErrorCode.Should().Be("InvalidResponse");
    }

    [TestMethod]
    public async Task CreateAgent_WritesDocumentedRequestBody()
    {
        handler.EnqueueJson(
            HttpStatusCode.OK,
            TestData.AgentJson(status: "creating"));
        using var client = CreateClient();

        var result = await client.CreateAgentAsync(
            "echo-agent",
            TestData.Definition(),
            CancellationToken.None);

        result.Version.Should().Be("1");
        result.Status.Should().Be("creating");
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.Uri.AbsoluteUri.Should().Be(
            $"{TestData.ProjectEndpoint}/agents?api-version=v1");

        using var json = JsonDocument.Parse(request.Body!);
        json.RootElement.GetProperty("name").GetString().Should().Be("echo-agent");
        var definition = json.RootElement.GetProperty("definition");
        definition.GetProperty("kind").GetString().Should().Be("hosted");
        definition.GetProperty("container_configuration")
            .GetProperty("image").GetString()
            .Should().Be("registry.azurecr.io/agents/echo:1");
        definition.GetProperty("protocol_versions")[0]
            .GetProperty("protocol").GetString()
            .Should().Be("responses");
    }

    [TestMethod]
    public async Task WaitUntilActive_PollsCreatingVersion()
    {
        handler.EnqueueJson(
            HttpStatusCode.OK,
            TestData.VersionJson(status: "active"));
        using var client = CreateClient();

        var result = await client.WaitUntilActiveAsync(
            TestData.Version(status: "creating"),
            CancellationToken.None);

        result.Status.Should().Be("active");
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            $"{TestData.ProjectEndpoint}/agents/echo-agent/versions/1?api-version=v1");
    }

    [TestMethod]
    public async Task WaitUntilActive_SurfacesProvisioningFailure()
    {
        using var client = CreateClient();
        var failed = TestData.Version(
            status: "failed",
            error: new FoundryError
            {
                Code = "image_pull_failed",
                Message = "The image could not be pulled.",
            });

        var action = () => client.WaitUntilActiveAsync(failed, CancellationToken.None);

        await action.Should().ThrowAsync<AgentProvisioningException>()
            .WithMessage("*image could not be pulled*");
    }

    [TestMethod]
    public async Task WaitUntilActive_HonorsCancellation()
    {
        using var client = new FoundryRestClient(
            new HttpClient(handler, disposeHandler: false),
            credential,
            TestData.ProjectEndpoint,
            new FoundryClientOptions
            {
                PollInterval = TimeSpan.FromMinutes(1),
                ProvisioningTimeout = TimeSpan.FromMinutes(2),
            });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = () => client.WaitUntilActiveAsync(
            TestData.Version(status: "creating"),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [TestMethod]
    public async Task WaitUntilActive_TimesOut()
    {
        using var client = new FoundryRestClient(
            new HttpClient(handler, disposeHandler: false),
            credential,
            TestData.ProjectEndpoint,
            new FoundryClientOptions
            {
                PollInterval = TimeSpan.Zero,
                ProvisioningTimeout = TimeSpan.Zero,
            });

        var action = () => client.WaitUntilActiveAsync(
            TestData.Version(status: "creating"),
            CancellationToken.None);

        await action.Should().ThrowAsync<TimeoutException>();
    }

    [TestMethod]
    public async Task ApiError_PreservesServiceCodeAndMessage()
    {
        handler.EnqueueJson(
            HttpStatusCode.BadRequest,
            """{"error":{"code":"AcrImageNotFound","message":"Image does not exist."}}""");
        using var client = CreateClient();

        var action = () => client.GetAgentAsync("echo-agent", CancellationToken.None);

        var exception = await action.Should().ThrowAsync<FoundryApiException>();
        exception.Which.ErrorCode.Should().Be("AcrImageNotFound");
        exception.Which.Message.Should().Contain("Image does not exist.");
    }

    [TestMethod]
    public async Task DeleteAgent_TreatsNotFoundAsSuccess()
    {
        handler.Enqueue(HttpStatusCode.NotFound);
        using var client = CreateClient();

        var action = () => client.DeleteAgentAsync("echo-agent", CancellationToken.None);

        await action.Should().NotThrowAsync();
    }

    [TestMethod]
    public void InvalidProjectEndpoint_IsRejected()
    {
        var action = () => new FoundryRestClient(
            new HttpClient(handler, disposeHandler: false),
            credential,
            "https://example.com/not-a-project",
            new FoundryClientOptions());

        action.Should().Throw<ArgumentException>()
            .WithMessage("*projectEndpoint*");
    }

    private FoundryRestClient CreateClient() =>
        new(
            new HttpClient(handler, disposeHandler: false),
            credential,
            TestData.ProjectEndpoint,
            new FoundryClientOptions
            {
                PollInterval = TimeSpan.Zero,
                ProvisioningTimeout = TimeSpan.FromSeconds(1),
            });
}
