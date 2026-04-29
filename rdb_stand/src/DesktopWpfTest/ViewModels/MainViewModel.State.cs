using Dt1520.Authenticator.DesktopWpfTest.Services;
using Dt1520.Authenticator.DesktopWpfTest.Storage;

namespace Dt1520.Authenticator.DesktopWpfTest.ViewModels;

public sealed partial class MainViewModel
{
    private readonly ISettingsStore _settingsStore;
    private readonly IReferenceBackendClientFactory _clientFactory;
    private string _referenceBackendBaseUrl = "http://127.0.0.1:5188/";
    private string _externalUserId = string.Empty;
    private string _operationDisplayName = "Reference WPF protected operation";
    private string _dt1520BaseUrl = "https://admin.ghostring.ru:18443/";
    private string _tenantId = string.Empty;
    private string _applicationClientId = string.Empty;
    private string _clientId = string.Empty;
    private string _clientSecret = string.Empty;
    private string _callbackSigningSecret = string.Empty;
    private string _callbackUrl = string.Empty;
    private string _scope = "challenges:read challenges:write devices:read";
    private string _totpCode = string.Empty;
    private string _sessionId = string.Empty;
    private string _pollingPath = string.Empty;
    private string _approvalStatus = "Not started";
    private string _lastMessage = "Ready";
    private string _latencySummary = string.Empty;
    private bool _isCommitted;
    private bool _isBusy;

    public string ReferenceBackendBaseUrl
    {
        get => _referenceBackendBaseUrl;
        set => SetProperty(ref _referenceBackendBaseUrl, value);
    }

    public string ExternalUserId
    {
        get => _externalUserId;
        set
        {
            if (SetProperty(ref _externalUserId, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string OperationDisplayName
    {
        get => _operationDisplayName;
        set => SetProperty(ref _operationDisplayName, value);
    }

    public string Dt1520BaseUrl
    {
        get => _dt1520BaseUrl;
        set => SetProperty(ref _dt1520BaseUrl, value);
    }

    public string TenantId
    {
        get => _tenantId;
        set => SetProperty(ref _tenantId, value);
    }

    public string ApplicationClientId
    {
        get => _applicationClientId;
        set => SetProperty(ref _applicationClientId, value);
    }

    public string ClientId
    {
        get => _clientId;
        set => SetProperty(ref _clientId, value);
    }

    public string ClientSecret
    {
        get => _clientSecret;
        set => SetProperty(ref _clientSecret, value);
    }

    public string CallbackSigningSecret
    {
        get => _callbackSigningSecret;
        set => SetProperty(ref _callbackSigningSecret, value);
    }

    public string CallbackUrl
    {
        get => _callbackUrl;
        set => SetProperty(ref _callbackUrl, value);
    }

    public string Scope
    {
        get => _scope;
        set => SetProperty(ref _scope, value);
    }

    public string TotpCode
    {
        get => _totpCode;
        set
        {
            if (SetProperty(ref _totpCode, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string SessionId
    {
        get => _sessionId;
        private set => SetProperty(ref _sessionId, value);
    }

    public string PollingPath
    {
        get => _pollingPath;
        private set
        {
            if (SetProperty(ref _pollingPath, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public string ApprovalStatus
    {
        get => _approvalStatus;
        private set => SetProperty(ref _approvalStatus, value);
    }

    public string LastMessage
    {
        get => _lastMessage;
        private set => SetProperty(ref _lastMessage, value);
    }

    public string LatencySummary
    {
        get => _latencySummary;
        private set => SetProperty(ref _latencySummary, value);
    }

    public bool IsCommitted
    {
        get => _isCommitted;
        private set => SetProperty(ref _isCommitted, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStateChanged();
            }
        }
    }

    public AsyncRelayCommand LoadSettingsCommand { get; }

    public AsyncRelayCommand SaveSettingsCommand { get; }

    public AsyncRelayCommand StartApprovalCommand { get; }

    public AsyncRelayCommand PollStatusCommand { get; }

    public AsyncRelayCommand SubmitTotpCommand { get; }

    public AsyncRelayCommand ClearTotpCommand { get; }

    private bool CanStartApproval()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(ExternalUserId);
    }

    private bool CanPollStatus()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(PollingPath);
    }

    private bool CanSubmitTotp()
    {
        return !IsBusy
            && !string.IsNullOrWhiteSpace(SessionId)
            && !string.IsNullOrWhiteSpace(TotpCode);
    }

    private string NormalizeDisplayName()
    {
        return string.IsNullOrWhiteSpace(OperationDisplayName)
            ? "Reference WPF protected operation"
            : OperationDisplayName.Trim();
    }

    private static string FormatLatency(ReferenceLatencyTimestamps latency)
    {
        var values = new[]
        {
            Format("desktop", latency.DesktopSubmittedAtUtc),
            Format("challenge-request", latency.BackendChallengeRequestedAtUtc),
            Format("challenge-created", latency.ChallengeCreatedAtUtc),
            Format("callback", latency.CallbackReceivedAtUtc),
            Format("totp", latency.TotpSubmittedAtUtc),
            Format("terminal", latency.TerminalAtUtc),
        };

        return string.Join(Environment.NewLine, values.Where(static value => value.Length > 0));
    }

    private static string Format(string label, DateTimeOffset? value)
    {
        return value is null ? string.Empty : $"{label}: {value:O}";
    }

    private void RaiseCommandStateChanged()
    {
        LoadSettingsCommand.RaiseCanExecuteChanged();
        SaveSettingsCommand.RaiseCanExecuteChanged();
        StartApprovalCommand.RaiseCanExecuteChanged();
        PollStatusCommand.RaiseCanExecuteChanged();
        SubmitTotpCommand.RaiseCanExecuteChanged();
        ClearTotpCommand.RaiseCanExecuteChanged();
    }
}
