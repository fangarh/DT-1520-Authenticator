using System.Net;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class ClientErrorMappingTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, Dt1520AuthenticatorErrorKind.ValidationFailed)]
    [InlineData(HttpStatusCode.Unauthorized, Dt1520AuthenticatorErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, Dt1520AuthenticatorErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, Dt1520AuthenticatorErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Conflict, Dt1520AuthenticatorErrorKind.Conflict)]
    [InlineData((HttpStatusCode)429, Dt1520AuthenticatorErrorKind.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, Dt1520AuthenticatorErrorKind.ServerFailure)]
    public async Task AuthenticateAsyncMapsProblemDetails(HttpStatusCode statusCode, Dt1520AuthenticatorErrorKind expectedKind)
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.Problem(statusCode));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.AuthenticateAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedKind, result.Error?.Kind);
        Assert.Equal((int)statusCode, result.Error?.StatusCode);
        Assert.Equal("Problem title", result.Error?.Title);
        Assert.Equal("request-123", result.Error?.RequestId);
        Assert.Equal("trace-123", result.Error?.TraceId);
        Assert.Equal("one", Assert.Single(result.Error?.ValidationErrors?["field"] ?? []));
    }

    [Fact]
    public async Task AuthenticateAsyncDistinguishesTransportFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection failed with secret-one"));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.AuthenticateAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.TransportFailure, result.Error?.Kind);
        Assert.DoesNotContain("secret-one", result.Error?.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticateAsyncMapsInvalidProblemJsonToSanitizedStatusError()
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.InvalidProblem(HttpStatusCode.BadGateway));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.AuthenticateAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.ServerFailure, result.Error?.Kind);
        Assert.Equal((int)HttpStatusCode.BadGateway, result.Error?.StatusCode);
        Assert.Equal("Bad Gateway", result.Error?.Title);
    }

    [Fact]
    public async Task AuthenticateAsyncDistinguishesCallerCancellation()
    {
        var handler = new FakeHttpMessageHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return ClientTestResponses.Token("token-one", 3600);
        });
        using var client = ClientTestFactory.Create(handler);
        using var cts = new CancellationTokenSource();

        await cts.CancelAsync();
        var result = await client.AuthenticateAsync(cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.Canceled, result.Error?.Kind);
    }

    [Fact]
    public async Task AuthenticateAsyncDistinguishesTimeout()
    {
        var handler = new FakeHttpMessageHandler(async (_, _, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            return ClientTestResponses.Token("token-one", 3600);
        });
        using var client = ClientTestFactory.Create(handler, requestTimeout: TimeSpan.FromMilliseconds(10));

        var result = await client.AuthenticateAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.Timeout, result.Error?.Kind);
    }
}
