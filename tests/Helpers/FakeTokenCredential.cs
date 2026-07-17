using Azure.Core;

namespace FoundryExtension.Tests.Helpers;

internal sealed class FakeTokenCredential : TokenCredential
{
    public List<string[]> RequestedScopes { get; } = [];

    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        RequestedScopes.Add(requestContext.Scopes);
        return CreateToken();
    }

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken)
    {
        RequestedScopes.Add(requestContext.Scopes);
        return ValueTask.FromResult(CreateToken());
    }

    private static AccessToken CreateToken() =>
        new("test-token", DateTimeOffset.UtcNow.AddHours(1));
}

