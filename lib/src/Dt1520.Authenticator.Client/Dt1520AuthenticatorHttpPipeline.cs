using System.Net.Http.Headers;
using System.Text.Json;

namespace Dt1520.Authenticator.Client;

internal sealed class Dt1520AuthenticatorHttpPipeline
{
    private readonly HttpClient _httpClient;
    private readonly Dt1520AuthenticatorValidatedOptions _options;

    public Dt1520AuthenticatorHttpPipeline(HttpClient httpClient, Dt1520AuthenticatorValidatedOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public HttpRequestMessage CreateRequest(HttpMethod method, Uri requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AddUserAgent(request);
        return request;
    }

    public bool TryBuildRequestUri(string path, out Uri requestUri)
    {
        requestUri = Uri.TryCreate(path, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(_options.BaseUrl, path.TrimStart('/'));

        return _options.BaseUrl.IsBaseOf(requestUri);
    }

    public async Task<Dt1520AuthenticatorResult<TResponse>> SendJsonAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var responseResult = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!responseResult.IsSuccess || responseResult.Value is null)
        {
            return Dt1520AuthenticatorResult<TResponse>.Failure(responseResult.Error!);
        }

        using var response = responseResult.Value;
        if (!response.IsSuccessStatusCode)
        {
            var error = await ProblemDetailsMapper.MapAsync(response, cancellationToken).ConfigureAwait(false);
            return Dt1520AuthenticatorResult<TResponse>.Failure(error);
        }

        return await ReadJsonResponseAsync<TResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dt1520AuthenticatorResult<HttpResponseMessage>> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_options.RequestTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
            return Dt1520AuthenticatorResult<HttpResponseMessage>.Success(response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Dt1520AuthenticatorResult<HttpResponseMessage>.Failure(ProblemDetailsMapper.CreateCanceled());
        }
        catch (OperationCanceledException)
        {
            return Dt1520AuthenticatorResult<HttpResponseMessage>.Failure(ProblemDetailsMapper.CreateTimeout());
        }
        catch (HttpRequestException)
        {
            return Dt1520AuthenticatorResult<HttpResponseMessage>.Failure(
                ProblemDetailsMapper.CreateTransportFailure("DT-1520 request failed before a response was received."));
        }
    }

    private static async Task<Dt1520AuthenticatorResult<TResponse>> ReadJsonResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var value = await JsonSerializer.DeserializeAsync<TResponse>(stream, ClientJson.Options, cancellationToken).ConfigureAwait(false);
            return value is null
                ? Dt1520AuthenticatorResult<TResponse>.Failure(ProblemDetailsMapper.CreateServerFailure("DT-1520 returned an empty response."))
                : Dt1520AuthenticatorResult<TResponse>.Success(value);
        }
        catch (JsonException)
        {
            return Dt1520AuthenticatorResult<TResponse>.Failure(ProblemDetailsMapper.CreateServerFailure("DT-1520 returned invalid JSON."));
        }
    }

    private void AddUserAgent(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_options.ProductName))
        {
            return;
        }

        request.Headers.UserAgent.Add(string.IsNullOrWhiteSpace(_options.ProductVersion)
            ? new ProductInfoHeaderValue(_options.ProductName)
            : new ProductInfoHeaderValue(_options.ProductName, _options.ProductVersion));
    }
}
