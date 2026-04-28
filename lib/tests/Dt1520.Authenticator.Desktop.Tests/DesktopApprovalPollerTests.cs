using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Dt1520.Authenticator.Desktop;

namespace Dt1520.Authenticator.Desktop.Tests;

public sealed class DesktopApprovalPollerTests
{
    [Fact]
    public async Task PollUntilCompletedReturnsTerminalSessionWithoutHttpCall()
    {
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(_ => Json("""{"status":"waiting"}""")));
        var poller = CreatePoller(httpClient);

        var outcome = await poller.PollUntilCompletedAsync(Session(DesktopApprovalSessionStatus.Approved));

        Assert.Equal(DesktopApprovalOutcomeKind.Approved, outcome.Kind);
        Assert.True(outcome.IsApproved);
        Assert.True(outcome.IsTerminal);
    }

    [Fact]
    public async Task PollUntilCompletedReadsBackendUntilApproved()
    {
        var responses = new Queue<HttpResponseMessage>([
            Json("""{"sessionId":"approval-1","status":"waiting","pollingPath":"/desktop/approvals/approval-1"}"""),
            Json("""{"sessionId":"approval-1","status":"approved","pollingPath":"/desktop/approvals/approval-1"}"""),
        ]);
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(_ => responses.Dequeue()));
        var poller = CreatePoller(httpClient);

        var outcome = await poller.PollUntilCompletedAsync(Session(DesktopApprovalSessionStatus.Waiting));

        Assert.Equal(DesktopApprovalOutcomeKind.Approved, outcome.Kind);
        Assert.True(outcome.IsApproved);
        Assert.Equal("approval-1", outcome.Session.SessionId);
    }

    [Theory]
    [InlineData("denied", DesktopApprovalOutcomeKind.Denied)]
    [InlineData("expired", DesktopApprovalOutcomeKind.Expired)]
    [InlineData("failed", DesktopApprovalOutcomeKind.Failed)]
    [InlineData("cancelled", DesktopApprovalOutcomeKind.Cancelled)]
    public async Task PollUntilCompletedMapsTerminalBackendStatuses(
        string backendStatus,
        DesktopApprovalOutcomeKind expectedKind)
    {
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(
            _ => Json($$"""{"sessionId":"approval-1","status":"{{backendStatus}}","pollingPath":"/desktop/approvals/approval-1"}""")));
        var poller = CreatePoller(httpClient);

        var outcome = await poller.PollUntilCompletedAsync(Session(DesktopApprovalSessionStatus.Waiting));

        Assert.Equal(expectedKind, outcome.Kind);
        Assert.True(outcome.IsTerminal);
        Assert.False(outcome.IsApproved);
    }

    [Fact]
    public async Task PollUntilCompletedReturnsCancelledOutcomeWhenCallerCancelsWait()
    {
        using var cancellation = new CancellationTokenSource();
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(_ =>
        {
            cancellation.Cancel();
            return Json("""{"sessionId":"approval-1","status":"waiting","pollingPath":"/desktop/approvals/approval-1"}""");
        }));
        var poller = CreatePoller(httpClient);

        var outcome = await poller.PollUntilCompletedAsync(
            Session(DesktopApprovalSessionStatus.Waiting),
            cancellation.Token);

        Assert.Equal(DesktopApprovalOutcomeKind.Cancelled, outcome.Kind);
        Assert.True(outcome.IsTerminal);
        Assert.Equal(DesktopApprovalSessionStatus.Cancelled, outcome.Session.Status);
    }

    [Fact]
    public async Task PollUntilCompletedReturnsTimedOutOutcomeWithoutThrowing()
    {
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(
            _ => Json("""{"sessionId":"approval-1","status":"waiting","pollingPath":"/desktop/approvals/approval-1"}""")));
        var poller = CreatePoller(
            httpClient,
            new DesktopApprovalPollingOptions
            {
                BackendBaseUrl = new Uri("https://desktop-backend.test"),
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromMilliseconds(10),
            });

        var outcome = await poller.PollUntilCompletedAsync(Session(DesktopApprovalSessionStatus.Waiting));

        Assert.Equal(DesktopApprovalOutcomeKind.TimedOut, outcome.Kind);
        Assert.True(outcome.IsTerminal);
        Assert.False(outcome.IsApproved);
    }

    [Fact]
    public async Task PollUntilCompletedDoesNotExposeRawBackendFailureBody()
    {
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("client_secret=do-not-leak", Encoding.UTF8, "text/plain"),
            }));
        var poller = CreatePoller(httpClient);

        var outcome = await poller.PollUntilCompletedAsync(Session(DesktopApprovalSessionStatus.Waiting));

        Assert.Equal(DesktopApprovalOutcomeKind.Failed, outcome.Kind);
        Assert.Equal(500, outcome.BackendStatusCode);
        Assert.DoesNotContain("client_secret", outcome.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("do-not-leak", outcome.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DesktopPublicOptionsDoNotContainSecretBearingConfiguration()
    {
        var publicProperties = typeof(DesktopApprovalPollingOptions)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains("BackendBaseUrl", publicProperties);
        Assert.DoesNotContain(publicProperties, name => name.Contains("Secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicProperties, name => name.Contains("Token", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicProperties, name => name.Contains("Bearer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicProperties, name => name.Contains("ClientSecret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DesktopProjectDoesNotReferenceDirectAuthenticatorClientPackage()
    {
        var project = XDocument.Load(SdkScaffold.FilePath("src", "Dt1520.Authenticator.Desktop", "Dt1520.Authenticator.Desktop.csproj"));
        var projectReferences = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(projectReferences, reference => reference.Contains("Dt1520.Authenticator.Client", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AbsolutePollingPathIsRejectedBeforeNetworkCall()
    {
        using var httpClient = new HttpClient(new DesktopFakeHttpMessageHandler(_ => Json("""{"status":"approved"}""")));
        var poller = CreatePoller(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => poller.PollUntilCompletedAsync(
            Session(DesktopApprovalSessionStatus.Waiting) with { PollingPath = "https://authenticator.example/api/v1/challenges/1" }));
    }

    private static DesktopApprovalPoller CreatePoller(
        HttpClient httpClient,
        DesktopApprovalPollingOptions? options = null)
    {
        return new DesktopApprovalPoller(
            httpClient,
            options ?? new DesktopApprovalPollingOptions
            {
                BackendBaseUrl = new Uri("https://desktop-backend.test"),
                PollInterval = TimeSpan.FromMilliseconds(1),
                Timeout = TimeSpan.FromSeconds(1),
            });
    }

    private static DesktopApprovalSession Session(DesktopApprovalSessionStatus status)
    {
        return new DesktopApprovalSession
        {
            SessionId = "approval-1",
            PollingPath = "/desktop/approvals/approval-1",
            Status = status,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
        };
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed class DesktopFakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public DesktopFakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _respond = respond;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(_respond(request));
    }
}
