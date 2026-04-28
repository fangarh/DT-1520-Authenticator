using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class ClientHttpFoundationTests
{
    [Fact]
    public async Task AuthenticateAsyncSendsClientCredentialsForm()
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.Token("token-one", 3600, "challenges:read"));
        using var client = ClientTestFactory.Create(handler, scope: "challenges:read");

        var result = await client.AuthenticateAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("token-one", result.Value?.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://auth.test/oauth2/token", request.Uri);
        Assert.DoesNotContain("Authorization", request.Headers.Select(header => header.Key));
        Assert.Contains("grant_type=client_credentials", request.Body, StringComparison.Ordinal);
        Assert.Contains("client_id=client-one", request.Body, StringComparison.Ordinal);
        Assert.Contains("client_secret=secret-one", request.Body, StringComparison.Ordinal);
        Assert.Contains("scope=challenges%3Aread", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsyncReusesCachedTokenBeforeExpirySkew()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero));
        var handler = new FakeHttpMessageHandler(request =>
        {
            var count = request.Requests.Count;
            return ClientTestResponses.Token($"token-{count}", 120);
        });

        using var client = ClientTestFactory.Create(handler, clock: clock, tokenExpirySkew: TimeSpan.FromSeconds(30));

        var first = await client.AuthenticateAsync();
        clock.UtcNow = clock.UtcNow.AddSeconds(60);
        var second = await client.AuthenticateAsync();

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("token-1", first.Value?.AccessToken);
        Assert.Equal("token-1", second.Value?.AccessToken);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AuthenticateAsyncRefreshesExpiredToken()
    {
        var clock = new MutableClock(new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero));
        var handler = new FakeHttpMessageHandler(request => ClientTestResponses.Token($"token-{request.Requests.Count}", 120));
        using var client = ClientTestFactory.Create(handler, clock: clock, tokenExpirySkew: TimeSpan.FromSeconds(30));

        var first = await client.AuthenticateAsync();
        clock.UtcNow = clock.UtcNow.AddSeconds(100);
        var second = await client.AuthenticateAsync();

        Assert.Equal("token-1", first.Value?.AccessToken);
        Assert.Equal("token-2", second.Value?.AccessToken);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SendAuthorizedJsonAsyncAttachesBearerOnlyToConfiguredBaseUrl()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json("""{"ok":true}"""));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.SendAuthorizedJsonAsync<TestResponse>(
            HttpMethod.Get,
            "/api/v1/ping",
            body: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Null(handler.Requests[0].Authorization);
        Assert.Equal("Bearer token-one", handler.Requests[1].Authorization);
        Assert.Equal("https://auth.test/api/v1/ping", handler.Requests[1].Uri);
    }

    [Fact]
    public async Task SendAuthorizedJsonAsyncRejectsAbsoluteUrlOutsideConfiguredBaseUrl()
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.Token("token-one", 3600));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.SendAuthorizedJsonAsync<TestResponse>(
            HttpMethod.Get,
            "https://evil.test/api/v1/ping",
            body: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.ValidationFailed, result.Error?.Kind);
        Assert.Empty(handler.Requests);
    }

    private sealed record TestResponse(bool Ok);
}
