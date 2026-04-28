using System.Text.Json;

namespace Dt1520.Authenticator.Client;

internal sealed class Dt1520AuthenticatorTokenProvider : IDisposable
{
    private readonly Dt1520AuthenticatorHttpPipeline _pipeline;
    private readonly Dt1520AuthenticatorValidatedOptions _options;
    private readonly IDt1520AuthenticatorClock _clock;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private Dt1520AuthenticatorAccessToken? _cachedToken;

    public Dt1520AuthenticatorTokenProvider(
        Dt1520AuthenticatorHttpPipeline pipeline,
        Dt1520AuthenticatorValidatedOptions options,
        IDt1520AuthenticatorClock clock)
    {
        _pipeline = pipeline;
        _options = options;
        _clock = clock;
    }

    public async Task<Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>> AuthenticateAsync(
        CancellationToken cancellationToken)
    {
        if (TryGetCachedToken(out var cachedToken))
        {
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Success(cachedToken);
        }

        if (!await TryWaitForRefreshAsync(cancellationToken).ConfigureAwait(false))
        {
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Failure(ProblemDetailsMapper.CreateCanceled());
        }

        try
        {
            if (TryGetCachedToken(out cachedToken))
            {
                return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Success(cachedToken);
            }

            var result = await RequestTokenAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                _cachedToken = result.Value;
            }

            return result;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void Dispose()
    {
        _tokenLock.Dispose();
    }

    private async Task<bool> TryWaitForRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _tokenLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>> RequestTokenAsync(
        CancellationToken cancellationToken)
    {
        using var request = _pipeline.CreateRequest(HttpMethod.Post, new Uri(_options.BaseUrl, "oauth2/token"));
        request.Content = new FormUrlEncodedContent(CreateTokenForm());
        var responseResult = await _pipeline.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!responseResult.IsSuccess || responseResult.Value is null)
        {
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Failure(responseResult.Error!);
        }

        using var response = responseResult.Value;
        if (!response.IsSuccessStatusCode)
        {
            var error = await ProblemDetailsMapper.MapAsync(response, cancellationToken).ConfigureAwait(false);
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Failure(error);
        }

        return await ReadTokenAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>> ReadTokenAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var token = await JsonSerializer.DeserializeAsync<TokenEndpointResponse>(stream, ClientJson.Options, cancellationToken).ConfigureAwait(false);
            return MapToken(token);
        }
        catch (JsonException)
        {
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Failure(
                ProblemDetailsMapper.CreateServerFailure("DT-1520 token endpoint returned invalid JSON."));
        }
    }

    private Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken> MapToken(TokenEndpointResponse? response)
    {
        if (response is null || string.IsNullOrWhiteSpace(response.AccessToken) || response.ExpiresIn <= 0)
        {
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Failure(
                ProblemDetailsMapper.CreateServerFailure("DT-1520 token endpoint returned an invalid token response."));
        }

        if (!string.Equals(response.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Failure(
                ProblemDetailsMapper.CreateServerFailure("DT-1520 token endpoint returned an unsupported token type."));
        }

        return Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>.Success(
            new Dt1520AuthenticatorAccessToken(
                response.AccessToken,
                "Bearer",
                _clock.UtcNow.AddSeconds(response.ExpiresIn),
                response.Scope));
    }

    private bool TryGetCachedToken(out Dt1520AuthenticatorAccessToken token)
    {
        token = _cachedToken!;
        return token is not null && token.ExpiresAtUtc - _options.TokenExpirySkew > _clock.UtcNow;
    }

    private IReadOnlyCollection<KeyValuePair<string, string>> CreateTokenForm()
    {
        var values = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", _options.Credentials.ClientId),
            new("client_secret", _options.Credentials.ClientSecret),
        };

        if (!string.IsNullOrWhiteSpace(_options.Scope))
        {
            values.Add(new KeyValuePair<string, string>("scope", _options.Scope));
        }

        return values;
    }
}
