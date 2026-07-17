using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace FoundryExtension.Tests.Helpers;

internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    AuthenticationHeaderValue? Authorization,
    string? Body);

internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public void EnqueueJson(HttpStatusCode statusCode, string json) =>
        responses.Enqueue(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

    public void Enqueue(HttpStatusCode statusCode) =>
        responses.Enqueue(new HttpResponseMessage(statusCode));

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri ?? throw new InvalidOperationException("Request URI was not set."),
            request.Headers.Authorization,
            body));

        if (responses.Count == 0)
        {
            throw new InvalidOperationException("No HTTP response was queued for the request.");
        }

        return responses.Dequeue();
    }
}

