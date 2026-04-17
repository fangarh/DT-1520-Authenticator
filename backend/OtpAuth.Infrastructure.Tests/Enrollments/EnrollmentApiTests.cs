using System.Net;
using System.Net.Http.Json;
using OtpAuth.Api.Enrollments;
using OtpAuth.Application.Enrollments;
using OtpAuth.Application.Factors;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Enrollments;

public sealed class EnrollmentApiTests
{
    [Fact]
    public async Task GetEnrollment_ReturnsUnauthorized_WhenRequestIsUnauthenticated()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedConfirmed();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEnrollment_ReturnsForbidden_WhenScopeIsMissing()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedConfirmed();
        using var client = factory.CreateAuthorizedClient(EnrollmentApiTestFactory.MissingScopeScenario);

        var response = await client.GetAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StartEnrollment_ReturnsProvisioningArtifact_AndLocationHeader()
    {
        await using var factory = new EnrollmentApiTestFactory();
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync("/api/v1/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            externalUserId = "user-start",
            issuer = "OTPAuth",
            label = "ivan.petrov",
        });

        var body = await response.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("pending", body!.Status);
        Assert.False(body.HasPendingReplacement);
        Assert.False(string.IsNullOrWhiteSpace(body.SecretUri));
        Assert.Equal(body.SecretUri, body.QrCodePayload);
        Assert.Equal($"/api/v1/enrollments/totp/{body.EnrollmentId}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task StartEnrollment_ReturnsConflict_WhenActiveEnrollmentAlreadyExists()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        store.SeedConfirmed("user-conflict");
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync("/api/v1/enrollments/totp", new
        {
            tenantId = EnrollmentApiTestContext.TenantId,
            externalUserId = "user-conflict",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetEnrollment_DoesNotReturnProvisioningArtifacts()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedConfirmed();
        using var client = factory.CreateAuthorizedClient();

        var response = await client.GetAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}");
        var body = await response.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("confirmed", body!.Status);
        Assert.False(body.HasPendingReplacement);
        Assert.Null(body.SecretUri);
        Assert.Null(body.QrCodePayload);
    }

    [Fact]
    public async Task ReplaceEnrollment_ReturnsConflict_WhenEnrollmentIsRevoked()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedRevoked();
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}/replace", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ReplaceEnrollment_StartsPendingReplacement_WithoutLeakingItFromReadEndpoint()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedConfirmed("user-replace");
        using var client = factory.CreateAuthorizedClient();

        var replaceResponse = await client.PostAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}/replace", content: null);
        var replaceBody = await replaceResponse.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();
        var readResponse = await client.GetAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}");
        var readBody = await readResponse.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, replaceResponse.StatusCode);
        Assert.NotNull(replaceBody);
        Assert.Equal("confirmed", replaceBody!.Status);
        Assert.True(replaceBody.HasPendingReplacement);
        Assert.False(string.IsNullOrWhiteSpace(replaceBody.SecretUri));
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.NotNull(readBody);
        Assert.True(readBody!.HasPendingReplacement);
        Assert.Null(readBody.SecretUri);
        Assert.Null(readBody.QrCodePayload);
    }

    [Fact]
    public async Task ConfirmEnrollment_ReturnsConflict_WhenAttemptLimitIsReached()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedPending(failedConfirmationAttempts: 4);
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsJsonAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}/confirm", new
        {
            code = "000000",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Too many invalid confirmation attempts", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfirmReplacement_PromotesPendingReplacement_WhenCodeIsValid()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedConfirmed("user-confirm-replace");
        using var client = factory.CreateAuthorizedClient();

        var replaceResponse = await client.PostAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}/replace", content: null);
        var replaceBody = await replaceResponse.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();
        var code = GenerateCurrentCode(replaceBody!.SecretUri!);

        var confirmResponse = await client.PostAsJsonAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}/confirm", new
        {
            code,
        });
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        Assert.NotNull(confirmBody);
        Assert.Equal("confirmed", confirmBody!.Status);
        Assert.False(confirmBody.HasPendingReplacement);
        Assert.Null(confirmBody.SecretUri);
        Assert.Null(confirmBody.QrCodePayload);
    }

    [Fact]
    public async Task RevokeEnrollment_ReturnsRevokedState_AndClearsPendingReplacement()
    {
        await using var factory = new EnrollmentApiTestFactory();
        var store = factory.GetStore();
        var enrollment = store.SeedConfirmed("user-revoke", new TotpPendingReplacementRecord
        {
            Secret = [21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40],
            Digits = 6,
            PeriodSeconds = 30,
            Algorithm = "SHA1",
            StartedUtc = DateTimeOffset.UtcNow,
            FailedConfirmationAttempts = 2,
        });
        using var client = factory.CreateAuthorizedClient();

        var response = await client.PostAsync($"/api/v1/enrollments/totp/{enrollment.EnrollmentId}/revoke", content: null);
        var body = await response.Content.ReadFromJsonAsync<TotpEnrollmentHttpResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("revoked", body!.Status);
        Assert.False(body.HasPendingReplacement);
        Assert.Null(body.SecretUri);
        Assert.Null(body.QrCodePayload);
    }

    private static string GenerateCurrentCode(string secretUri)
    {
        var parameters = ParseQueryParameters(secretUri);
        var secret = Base32Decode(parameters["secret"]);
        var digits = int.Parse(parameters["digits"]);
        var period = int.Parse(parameters["period"]);
        var algorithm = parameters["algorithm"];
        var timeStep = TotpCodeCalculator.GetTimeStep(DateTimeOffset.UtcNow, period);

        return TotpCodeCalculator.GenerateCode(secret, digits, algorithm, timeStep);
    }

    private static Dictionary<string, string> ParseQueryParameters(string uri)
    {
        var query = new Uri(uri).Query.TrimStart('?');
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            parameters[key] = value;
        }

        return parameters;
    }

    private static byte[] Base32Decode(string encoded)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        var normalized = encoded.TrimEnd('=').ToUpperInvariant();
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var character in normalized)
        {
            var value = alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new InvalidOperationException($"Unexpected base32 character '{character}'.");
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return [.. bytes];
    }
}
