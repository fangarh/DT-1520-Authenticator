using System.Net;
using System.Text;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

internal static class ClientTestFactory
{
    public static Dt1520AuthenticatorClient Create(
        FakeHttpMessageHandler handler,
        string? scope = null,
        MutableClock? clock = null,
        TimeSpan? tokenExpirySkew = null,
        TimeSpan? requestTimeout = null)
    {
        var options = new Dt1520AuthenticatorClientOptions
        {
            BaseUrl = new Uri("https://auth.test"),
            Credentials = new Dt1520AuthenticatorClientCredentials("client-one", "secret-one"),
            Scope = scope,
            TokenExpirySkew = tokenExpirySkew ?? TimeSpan.FromMinutes(1),
            RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(30),
            ProductName = "dt1520-test",
            ProductVersion = "0.1.0",
        };

        var httpClient = new HttpClient(handler);
        return new Dt1520AuthenticatorClient(httpClient, options, clock ?? new MutableClock(), disposeHttpClient: true);
    }
}

internal static class ClientTestResponses
{
    public static HttpResponseMessage Token(string token, int expiresIn, string? scope = null)
    {
        var scopeJson = scope is null ? string.Empty : $",\"scope\":\"{scope}\"";
        return Json($"{{\"access_token\":\"{token}\",\"token_type\":\"Bearer\",\"expires_in\":{expiresIn}{scopeJson}}}");
    }

    public static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage Problem(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                """{"type":"https://otpauth.dev/problems/test","title":"Problem title","detail":"Problem detail","traceId":"trace-123","errors":{"field":["one"]}}""",
                Encoding.UTF8,
                "application/problem+json"),
            Headers =
            {
                { "X-Request-Id", "request-123" },
            },
        };
    }

    public static HttpResponseMessage InvalidProblem(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/problem+json"),
        };
    }
}

internal sealed class MutableClock : IDt1520AuthenticatorClock
{
    public MutableClock()
        : this(new DateTimeOffset(2026, 4, 27, 10, 0, 0, TimeSpan.Zero))
    {
    }

    public MutableClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<FakeHttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly List<CapturedRequest> _requests = [];

    public FakeHttpMessageHandler(Func<FakeHttpMessageHandler, HttpResponseMessage> respond)
        : this((handler, _, _) => Task.FromResult(respond(handler)))
    {
    }

    public FakeHttpMessageHandler(
        Func<FakeHttpMessageHandler, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    public IReadOnlyList<CapturedRequest> Requests => _requests;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Add(new CapturedRequest(
            request.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            request.Headers.Authorization?.ToString(),
            request.Headers.ToDictionary(header => header.Key, header => header.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            body));

        return await _respond(this, request, cancellationToken);
    }
}

internal sealed record CapturedRequest(
    HttpMethod Method,
    string Uri,
    string? Authorization,
    IReadOnlyDictionary<string, string[]> Headers,
    string Body);
