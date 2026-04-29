using Dt1520.Authenticator.Desktop;
using Dt1520.Authenticator.DesktopWpfTest.Models;
using Dt1520.Authenticator.DesktopWpfTest.Services;
using Dt1520.Authenticator.DesktopWpfTest.Storage;
using Dt1520.Authenticator.DesktopWpfTest.ViewModels;

namespace Dt1520.Authenticator.DesktopWpfTest.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public async Task SaveSettingsAsync_PersistsDemoConfigButNotTotpCode()
    {
        var store = new CapturingStore();
        var viewModel = new MainViewModel(store, new StubClientFactory());
        viewModel.ExternalUserId = " user-1 ";
        viewModel.ClientSecret = "demo-secret";
        viewModel.TotpCode = "123456";

        await viewModel.SaveSettingsAsync();

        Assert.NotNull(store.Saved);
        Assert.Equal("user-1", store.Saved.ExternalUserId);
        Assert.Equal("demo-secret", store.Saved.ClientSecret);
        Assert.DoesNotContain("123456", store.Saved.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartApprovalAsync_UpdatesVisibleSessionState()
    {
        var client = new StubClient(new ReferenceApprovalSession
        {
            SessionId = "session-1",
            PollingPath = "/api/reference/operations/session-1/status",
            Status = DesktopApprovalSessionStatus.Waiting,
            IsCommitted = false,
        });
        var viewModel = new MainViewModel(new CapturingStore(), new StubClientFactory(client));
        viewModel.ExternalUserId = "user-1";

        await viewModel.StartApprovalAsync();

        Assert.Equal("session-1", viewModel.SessionId);
        Assert.Equal("/api/reference/operations/session-1/status", viewModel.PollingPath);
        Assert.Equal("Waiting", viewModel.ApprovalStatus);
    }

    private sealed class CapturingStore : ISettingsStore
    {
        public DemoSettings? Saved { get; private set; }

        public Task<DemoSettings?> LoadAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<DemoSettings?>(Saved);
        }

        public Task SaveAsync(DemoSettings settings, CancellationToken cancellationToken = default)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubClientFactory : IReferenceBackendClientFactory
    {
        private readonly IReferenceBackendClient _client;

        public StubClientFactory()
            : this(new StubClient(null))
        {
        }

        public StubClientFactory(IReferenceBackendClient client)
        {
            _client = client;
        }

        public IReferenceBackendClient Create(Uri backendBaseUrl)
        {
            return _client;
        }
    }

    private sealed class StubClient : IReferenceBackendClient
    {
        private readonly ReferenceApprovalSession? _session;

        public StubClient(ReferenceApprovalSession? session)
        {
            _session = session;
        }

        public Task<ReferenceBackendResult<ReferenceApprovalSession>> StartOperationAsync(
            string externalUserId,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result());
        }

        public Task<ReferenceBackendResult<ReferenceApprovalSession>> GetStatusAsync(
            string pollingPath,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result());
        }

        public Task<ReferenceBackendResult<ReferenceApprovalSession>> SubmitTotpAsync(
            string sessionId,
            string code,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result());
        }

        private ReferenceBackendResult<ReferenceApprovalSession> Result()
        {
            return _session is null
                ? ReferenceBackendResult<ReferenceApprovalSession>.Failure("No session configured.")
                : ReferenceBackendResult<ReferenceApprovalSession>.Success(_session);
        }
    }
}
