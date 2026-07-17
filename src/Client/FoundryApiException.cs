using System.Net;

namespace FoundryExtension.Client;

internal sealed class FoundryApiException(
    HttpStatusCode statusCode,
    string? errorCode,
    string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    public string? ErrorCode { get; } = errorCode;
}

internal sealed class AgentProvisioningException(string? errorCode, string message)
    : Exception(message)
{
    public string? ErrorCode { get; } = errorCode;
}

