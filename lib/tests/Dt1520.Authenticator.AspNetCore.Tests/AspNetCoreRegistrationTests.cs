using Dt1520.Authenticator.AspNetCore;
using Dt1520.Authenticator.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dt1520.Authenticator.AspNetCore.Tests;

public sealed class AspNetCoreRegistrationTests
{
    [Fact]
    public void AddDt1520AuthenticatorBindsOptionsAndRegistersServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BaseUrl"] = "https://auth.test/",
                ["ClientId"] = "client-one",
                ["ClientSecret"] = "secret-one",
                ["CallbackSigningSecret"] = "callback-secret",
                ["Scope"] = "challenges:write",
                ["ProductName"] = "IntegratorBackend",
                ["ProductVersion"] = "1.2.3",
            })
            .Build();

        services.AddDt1520Authenticator(configuration);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var options = provider.GetRequiredService<IOptions<Dt1520AuthenticatorAspNetCoreOptions>>().Value;

        Assert.Equal(new Uri("https://auth.test/"), options.BaseUrl);
        Assert.Equal("client-one", options.ClientId);
        Assert.Equal("challenges:write", options.Scope);
        Assert.NotNull(provider.GetRequiredService<Dt1520AuthenticatorClient>());
        Assert.NotNull(provider.GetRequiredService<Dt1520AuthenticatorCallbackValidator>());
    }

    [Fact]
    public async Task AddDt1520AuthenticatorUsesConfigurableNamedHttpClient()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse("""{"access_token":"token-one","token_type":"Bearer","expires_in":3600}"""));
        var services = new ServiceCollection();

        services.AddDt1520Authenticator(options =>
        {
            options.BaseUrl = new Uri("https://auth.test/");
            options.ClientId = "client-one";
            options.ClientSecret = "secret-one";
            options.CallbackSigningSecret = "callback-secret";
        }).ConfigurePrimaryHttpMessageHandler(() => handler);

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var client = provider.GetRequiredService<Dt1520AuthenticatorClient>();

        var result = await client.AuthenticateAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("token-one", result.Value?.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://auth.test/oauth2/token", request.Uri);
        Assert.Contains("client_secret=secret-one", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsValidationRejectsMissingSecretsWithoutEchoingSecretValues()
    {
        var services = new ServiceCollection();
        services.AddDt1520Authenticator(options =>
        {
            options.BaseUrl = new Uri("https://auth.test/");
            options.ClientId = "client-one";
            options.ClientSecret = "secret-one";
        });

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<Dt1520AuthenticatorAspNetCoreOptions>>().Value);

        Assert.Contains("CallbackSigningSecret is required", string.Join(" ", exception.Failures), StringComparison.Ordinal);
        Assert.DoesNotContain("secret-one", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OptionsToStringRedactsSecretValues()
    {
        var options = new Dt1520AuthenticatorAspNetCoreOptions
        {
            BaseUrl = new Uri("https://auth.test/"),
            ClientId = "client-one",
            ClientSecret = "secret-one",
            CallbackSigningSecret = "callback-secret",
        };

        var text = options.ToString();

        Assert.DoesNotContain("secret-one", text, StringComparison.Ordinal);
        Assert.DoesNotContain("callback-secret", text, StringComparison.Ordinal);
        Assert.Contains("[redacted]", text, StringComparison.Ordinal);
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var recorded = new RecordedRequest(
                request.Method,
                request.RequestUri?.AbsoluteUri ?? string.Empty,
                body);
            Requests.Add(recorded);
            return respond(recorded);
        }
    }

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string Body);
}
