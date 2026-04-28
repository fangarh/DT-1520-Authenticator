using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Dt1520.Authenticator.Client;

/// <summary>
/// Framework-agnostic DT-1520 Authenticator HTTP client.
/// </summary>
public sealed class Dt1520AuthenticatorClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Dt1520AuthenticatorHttpPipeline _pipeline;
    private readonly Dt1520AuthenticatorTokenProvider _tokenProvider;
    private readonly bool _disposeHttpClient;

    /// <summary>
    /// Creates a DT-1520 client with an internally owned <see cref="HttpClient"/>.
    /// </summary>
    public Dt1520AuthenticatorClient(Dt1520AuthenticatorClientOptions options)
        : this(CreateDefaultHttpClient(), options, SystemDt1520AuthenticatorClock.Instance, disposeHttpClient: true)
    {
    }

    /// <summary>
    /// Creates a DT-1520 client using a caller-provided <see cref="HttpClient"/>.
    /// </summary>
    public Dt1520AuthenticatorClient(HttpClient httpClient, Dt1520AuthenticatorClientOptions options)
        : this(httpClient, options, SystemDt1520AuthenticatorClock.Instance, disposeHttpClient: false)
    {
    }

    internal Dt1520AuthenticatorClient(
        HttpClient httpClient,
        Dt1520AuthenticatorClientOptions options,
        IDt1520AuthenticatorClock clock,
        bool disposeHttpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        var validatedOptions = options.Validate();
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        _pipeline = new Dt1520AuthenticatorHttpPipeline(_httpClient, validatedOptions);
        _tokenProvider = new Dt1520AuthenticatorTokenProvider(_pipeline, validatedOptions, clock);
        _disposeHttpClient = disposeHttpClient;
    }

    /// <summary>
    /// Acquires or reuses an OAuth 2.0 access token for the configured integration client.
    /// </summary>
    public Task<Dt1520AuthenticatorResult<Dt1520AuthenticatorAccessToken>> AuthenticateAsync(
        CancellationToken cancellationToken = default)
    {
        return _tokenProvider.AuthenticateAsync(cancellationToken);
    }

    /// <summary>
    /// Creates a second-factor challenge.
    /// </summary>
    public Task<Dt1520AuthenticatorResult<ChallengeResponse>> CreateChallengeAsync(
        CreateChallengeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validationError = ChallengeRequestValidator.ValidateCreate(request);
        if (validationError is not null)
        {
            return Task.FromResult(Dt1520AuthenticatorResult<ChallengeResponse>.Failure(validationError));
        }

        return SendAuthorizedJsonAsync<ChallengeResponse>(
            HttpMethod.Post,
            "/api/v1/challenges",
            CreateChallengeHttpBody.FromRequest(request),
            configureRequest: httpRequest =>
            {
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    httpRequest.Headers.Add("Idempotency-Key", request.IdempotencyKey);
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets the current state of a challenge.
    /// </summary>
    public Task<Dt1520AuthenticatorResult<ChallengeResponse>> GetChallengeAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        var validationError = ChallengeRequestValidator.ValidateChallengeId(challengeId);
        if (validationError is not null)
        {
            return Task.FromResult(Dt1520AuthenticatorResult<ChallengeResponse>.Failure(validationError));
        }

        return SendAuthorizedJsonAsync<ChallengeResponse>(
            HttpMethod.Get,
            $"/api/v1/challenges/{challengeId:D}",
            body: null,
            cancellationToken);
    }

    /// <summary>
    /// Verifies a six-digit TOTP code against an active challenge.
    /// </summary>
    public Task<Dt1520AuthenticatorResult<ChallengeResponse>> VerifyTotpAsync(
        Guid challengeId,
        VerifyTotpRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ChallengeRequestValidator.ValidateVerifyTotp(challengeId, request);
        if (validationError is not null)
        {
            return Task.FromResult(Dt1520AuthenticatorResult<ChallengeResponse>.Failure(validationError));
        }

        return SendAuthorizedJsonAsync<ChallengeResponse>(
            HttpMethod.Post,
            $"/api/v1/challenges/{challengeId:D}/verify-totp",
            request,
            cancellationToken);
    }

    /// <summary>
    /// Lists active devices available to the configured integration client for explicit routing.
    /// </summary>
    public Task<Dt1520AuthenticatorResult<IReadOnlyCollection<DeviceRoutingCandidate>>> ListDevicesForRoutingAsync(
        string externalUserId,
        bool pushCapableOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
        {
            return Task.FromResult(Dt1520AuthenticatorResult<IReadOnlyCollection<DeviceRoutingCandidate>>.Failure(
                ProblemDetailsMapper.CreateValidationFailure("ExternalUserId is required.")));
        }

        var encodedExternalUserId = Uri.EscapeDataString(externalUserId.Trim());
        var pushCapableOnlyValue = pushCapableOnly ? "true" : "false";

        return SendAuthorizedJsonAsync<IReadOnlyCollection<DeviceRoutingCandidate>>(
            HttpMethod.Get,
            $"/api/v1/devices?externalUserId={encodedExternalUserId}&pushCapableOnly={pushCapableOnlyValue}",
            body: null,
            cancellationToken);
    }

    /// <summary>
    /// Finds a single active push-capable device for explicit push challenge routing.
    /// </summary>
    public async Task<Dt1520AuthenticatorResult<PushDeviceSelectionResult>> SelectSinglePushDeviceAsync(
        string externalUserId,
        CancellationToken cancellationToken = default)
    {
        var devicesResult = await ListDevicesForRoutingAsync(
            externalUserId,
            pushCapableOnly: true,
            cancellationToken).ConfigureAwait(false);
        if (!devicesResult.IsSuccess || devicesResult.Value is null)
        {
            return Dt1520AuthenticatorResult<PushDeviceSelectionResult>.Failure(devicesResult.Error!);
        }

        return Dt1520AuthenticatorResult<PushDeviceSelectionResult>.Success(
            PushDeviceSelectionResult.FromCandidates(devicesResult.Value));
    }

    internal async Task<Dt1520AuthenticatorResult<TResponse>> SendAuthorizedJsonAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        return await SendAuthorizedJsonAsync<TResponse>(
            method,
            path,
            body,
            configureRequest: null,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<Dt1520AuthenticatorResult<TResponse>> SendAuthorizedJsonAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        Action<HttpRequestMessage>? configureRequest,
        CancellationToken cancellationToken)
    {
        if (!_pipeline.TryBuildRequestUri(path, out var requestUri))
        {
            return Dt1520AuthenticatorResult<TResponse>.Failure(
                ProblemDetailsMapper.CreateValidationFailure("Request path must stay under the configured DT-1520 base URL."));
        }

        var tokenResult = await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        if (!tokenResult.IsSuccess || tokenResult.Value is null)
        {
            return Dt1520AuthenticatorResult<TResponse>.Failure(tokenResult.Error!);
        }

        using var request = _pipeline.CreateRequest(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value.AccessToken);
        configureRequest?.Invoke(request);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ClientJson.Options);
        }

        return await _pipeline.SendJsonAsync<TResponse>(request, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _tokenProvider.Dispose();
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        };

        return new HttpClient(handler, disposeHandler: true);
    }

    private sealed record CreateChallengeHttpBody(
        Guid TenantId,
        Guid ApplicationClientId,
        ChallengeSubject Subject,
        ChallengeOperation Operation,
        IReadOnlyCollection<ChallengeFactorType>? PreferredFactors,
        Guid? TargetDeviceId,
        ChallengeCallbackRegistration? Callback,
        string? CorrelationId)
    {
        public static CreateChallengeHttpBody FromRequest(CreateChallengeRequest request)
        {
            return new CreateChallengeHttpBody(
                request.TenantId,
                request.ApplicationClientId,
                request.Subject,
                request.Operation,
                request.PreferredFactors,
                request.TargetDeviceId,
                request.Callback,
                request.CorrelationId);
        }
    }
}
