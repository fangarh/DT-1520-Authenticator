using System.Net;
using System.Text;
using Dt1520.Authenticator.DesktopWpfTest.Services;

namespace Dt1520.Authenticator.DesktopWpfTest.Tests;

public sealed class ReferenceBackendClientTests
{
    [Fact]
    public async Task StartOperationAsync_PostsExpectedRequest()
    {
        using var handler = new RecordingHandler(_ => SessionResponse());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5188/"),
        };
        var client = new ReferenceBackendClient(httpClient);

        var result = await client.StartOperationAsync("user-1", "Sensitive operation");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/reference/operations", handler.Path);
        Assert.Contains("\"externalUserId\":\"user-1\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"displayName\":\"Sensitive operation\"", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatusAsync_RejectsAbsolutePollingPath()
    {
        using var handler = new RecordingHandler(_ => SessionResponse());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5188/"),
        };
        var client = new ReferenceBackendClient(httpClient);

        var result = await client.GetStatusAsync("https://evil.example.test/status");

        Assert.False(result.IsSuccess);
        Assert.Null(handler.Path);
    }

    [Fact]
    public async Task SubmitTotpAsync_PostsCodeToSessionEndpoint()
    {
        using var handler = new RecordingHandler(_ => SessionResponse());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:5188/"),
        };
        var client = new ReferenceBackendClient(httpClient);

        var result = await client.SubmitTotpAsync("session 1", "123456");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("/api/reference/operations/session%201/totp", handler.Path);
        Assert.Contains("\"code\":\"123456\"", handler.Body, StringComparison.Ordinal);
    }

    private static HttpResponseMessage SessionResponse()
    {
        var json = """
            {
              "sessionId": "session-1",
              "pollingPath": "/api/reference/operations/session-1/status",
              "status": "Waiting",
              "isCommitted": false,
              "latency": {}
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public HttpMethod? Method { get; private set; }

        public string? Path { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri?.PathAndQuery;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return _responseFactory(request);
        }
    }
}
