using System.Net;
using System.Reflection;
using Dt1520.Authenticator.Client;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class ClientDeviceRoutingTests
{
    private static readonly Guid DeviceId = Guid.Parse("58f1797e-885b-44ea-9217-e630c7188d8a");
    private static readonly Guid SecondDeviceId = Guid.Parse("16fd71ec-2d41-41d6-9bbb-2dbb293c67e1");
    private static readonly Guid ChallengeId = Guid.Parse("52c7b0a4-bc5a-4a3c-86ec-1dd260f264d5");
    private static readonly Guid TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
    private static readonly Guid ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");

    [Fact]
    public async Task ListDevicesForRoutingAsyncSendsEncodedQueryAndMapsSafeDeviceMetadata()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json(DeviceListJson()));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.ListDevicesForRoutingAsync("user+routing", pushCapableOnly: true);

        Assert.True(result.IsSuccess);
        var device = Assert.Single(result.Value!);
        Assert.Equal(DeviceId, device.Id);
        Assert.Equal(DevicePlatform.Android, device.Platform);
        Assert.Equal(DeviceStatus.Active, device.Status);
        Assert.Equal(DeviceAttestationStatus.NotProvided, device.AttestationStatus);
        Assert.Equal("Pixel Work", device.DeviceName);
        Assert.True(device.IsPushCapable);
        Assert.Equal(new DateTimeOffset(2026, 4, 27, 9, 58, 0, TimeSpan.Zero), device.LastSeenAt);

        Assert.Equal("https://auth.test/api/v1/devices?externalUserId=user%2Brouting&pushCapableOnly=true", handler.Requests[1].Uri);
        Assert.Equal("Bearer token-one", handler.Requests[1].Authorization);
        Assert.Empty(handler.Requests[1].Body);
    }

    [Fact]
    public async Task ListDevicesForRoutingAsyncRejectsBlankExternalUserIdBeforeNetwork()
    {
        var handler = new FakeHttpMessageHandler(_ => ClientTestResponses.Token("token-one", 3600));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.ListDevicesForRoutingAsync(" ");

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.ValidationFailed, result.Error?.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SelectSinglePushDeviceAsyncReturnsSelectedDeviceWhenExactlyOnePushCandidateExists()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json(DeviceListJson()));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.SelectSinglePushDeviceAsync("user-routing");

        Assert.True(result.IsSuccess);
        Assert.True(result.Value?.IsSelected);
        Assert.Equal(PushDeviceSelectionStatus.Selected, result.Value?.Status);
        Assert.Equal(DeviceId, result.Value?.Device?.Id);
        Assert.Equal("https://auth.test/api/v1/devices?externalUserId=user-routing&pushCapableOnly=true", handler.Requests[1].Uri);
    }

    [Fact]
    public async Task SelectSinglePushDeviceAsyncReturnsNoDeviceOutcomeForEmptyList()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json("[]"));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.SelectSinglePushDeviceAsync("user-no-device");

        Assert.True(result.IsSuccess);
        Assert.Equal(PushDeviceSelectionStatus.NoActivePushCapableDevice, result.Value?.Status);
        Assert.False(result.Value?.IsSelected);
        Assert.Null(result.Value?.Device);
        Assert.Equal(0, result.Value?.MatchingDeviceCount);
    }

    [Fact]
    public async Task SelectSinglePushDeviceAsyncReturnsAmbiguousOutcomeForMultiplePushCandidates()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Json(DeviceListJson(includeSecondDevice: true)));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.SelectSinglePushDeviceAsync("user-ambiguous");

        Assert.True(result.IsSuccess);
        Assert.Equal(PushDeviceSelectionStatus.AmbiguousActivePushCapableDevices, result.Value?.Status);
        Assert.False(result.Value?.IsSelected);
        Assert.Null(result.Value?.Device);
        Assert.Equal(2, result.Value?.MatchingDeviceCount);
    }

    [Fact]
    public async Task DeviceLookupMapsExpectedProblemDetails()
    {
        var handler = new FakeHttpMessageHandler(request =>
            request.Requests.Count == 1
                ? ClientTestResponses.Token("token-one", 3600)
                : ClientTestResponses.Problem(HttpStatusCode.Forbidden));
        using var client = ClientTestFactory.Create(handler);

        var result = await client.ListDevicesForRoutingAsync("user-forbidden", pushCapableOnly: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(Dt1520AuthenticatorErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal((int)HttpStatusCode.Forbidden, result.Error?.StatusCode);
    }

    [Fact]
    public void PushDeviceSelectionResultFiltersOutInactiveOrTotpOnlyDevices()
    {
        var result = PushDeviceSelectionResult.FromCandidates(
        [
            new DeviceRoutingCandidate
            {
                Id = DeviceId,
                Platform = DevicePlatform.Android,
                Status = DeviceStatus.Active,
                AttestationStatus = DeviceAttestationStatus.NotProvided,
                IsPushCapable = false,
            },
            new DeviceRoutingCandidate
            {
                Id = SecondDeviceId,
                Platform = DevicePlatform.Android,
                Status = DeviceStatus.Revoked,
                AttestationStatus = DeviceAttestationStatus.NotProvided,
                IsPushCapable = true,
            },
        ]);

        Assert.Equal(PushDeviceSelectionStatus.NoActivePushCapableDevice, result.Status);
        Assert.Equal(0, result.MatchingDeviceCount);
    }

    [Theory]
    [InlineData(ChallengeStatus.Pending, PushChallengeOutcomeKind.Pending, false)]
    [InlineData(ChallengeStatus.Approved, PushChallengeOutcomeKind.Approved, true)]
    [InlineData(ChallengeStatus.Denied, PushChallengeOutcomeKind.Denied, true)]
    [InlineData(ChallengeStatus.Expired, PushChallengeOutcomeKind.Expired, true)]
    [InlineData(ChallengeStatus.Failed, PushChallengeOutcomeKind.Failed, true)]
    public void PushChallengeOutcomeMapsTerminalStates(
        ChallengeStatus status,
        PushChallengeOutcomeKind expectedKind,
        bool expectedTerminal)
    {
        var outcome = PushChallengeOutcome.FromChallenge(new ChallengeResponse
        {
            Id = ChallengeId,
            TenantId = TenantId,
            ApplicationClientId = ApplicationClientId,
            FactorType = ChallengeFactorType.Push,
            Status = status,
            ExpiresAt = new DateTimeOffset(2026, 4, 27, 10, 5, 0, TimeSpan.Zero),
            TargetDeviceId = DeviceId,
        });

        Assert.Equal(expectedKind, outcome.Kind);
        Assert.Equal(expectedTerminal, outcome.IsTerminal);
    }

    [Fact]
    public void DeviceRoutingPublicModelDoesNotExposeMobileSecrets()
    {
        var propertyNames = typeof(DeviceRoutingCandidate)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(static property => property.Name)
            .ToArray();

        Assert.DoesNotContain("PushToken", propertyNames);
        Assert.DoesNotContain("PublicKey", propertyNames);
        Assert.DoesNotContain("InstallationId", propertyNames);
        Assert.DoesNotContain("AccessToken", propertyNames);
        Assert.DoesNotContain("RefreshToken", propertyNames);

        var device = new DeviceRoutingCandidate
        {
            Id = DeviceId,
            Platform = DevicePlatform.Android,
            Status = DeviceStatus.Active,
            AttestationStatus = DeviceAttestationStatus.NotProvided,
            DeviceName = "User private phone label",
            IsPushCapable = true,
        };

        Assert.DoesNotContain("User private phone label", device.ToString(), StringComparison.Ordinal);
    }

    private static string DeviceListJson(bool includeSecondDevice = false)
    {
        var secondDeviceJson = includeSecondDevice
            ? $$"""
                ,
                {
                  "id": "{{SecondDeviceId}}",
                  "platform": "android",
                  "status": "active",
                  "attestationStatus": "accepted",
                  "deviceName": "Pixel Backup",
                  "isPushCapable": true,
                  "activatedAt": "2026-04-27T08:00:00Z",
                  "lastSeenAt": "2026-04-27T09:59:00Z"
                }
                """
            : string.Empty;

        return $$"""
            [
              {
                "id": "{{DeviceId}}",
                "platform": "android",
                "status": "active",
                "attestationStatus": "not_provided",
                "deviceName": "Pixel Work",
                "isPushCapable": true,
                "activatedAt": "2026-04-27T08:00:00Z",
                "lastSeenAt": "2026-04-27T09:58:00Z"
              }{{secondDeviceJson}}
            ]
            """;
    }
}
