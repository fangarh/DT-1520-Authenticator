using System.IO;
using System.Net.Http;
using Dt1520.Authenticator.DesktopWpfTest.Models;
using Dt1520.Authenticator.DesktopWpfTest.Services;
using Dt1520.Authenticator.DesktopWpfTest.Storage;

namespace Dt1520.Authenticator.DesktopWpfTest.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(ISettingsStore settingsStore, IReferenceBackendClientFactory clientFactory)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));

        LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync, () => !IsBusy);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => !IsBusy);
        StartApprovalCommand = new AsyncRelayCommand(StartApprovalAsync, CanStartApproval);
        PollStatusCommand = new AsyncRelayCommand(PollStatusAsync, CanPollStatus);
        SubmitTotpCommand = new AsyncRelayCommand(SubmitTotpAsync, CanSubmitTotp);
        ClearTotpCommand = new AsyncRelayCommand(ClearTotpAsync, () => !IsBusy);
    }

    public async Task LoadSettingsAsync()
    {
        await RunAsync(async () =>
        {
            var settings = await _settingsStore.LoadAsync();
            ApplySettings(settings ?? new DemoSettings());
            LastMessage = settings is null ? "No local settings file yet." : "Local settings loaded.";
        });
    }

    public async Task SaveSettingsAsync()
    {
        await RunAsync(async () =>
        {
            await _settingsStore.SaveAsync(CreateSettings());
            LastMessage = "Local demo settings saved to encrypted JSON. TOTP code was not saved.";
        });
    }

    public async Task StartApprovalAsync()
    {
        await RunAsync(async () =>
        {
            if (!TryCreateClient(out var client))
            {
                return;
            }

            var result = await client.StartOperationAsync(
                ExternalUserId.Trim(),
                NormalizeDisplayName());

            ApplyBackendResult(result, "Approval session started.");
        });
    }

    public async Task PollStatusAsync()
    {
        await RunAsync(async () =>
        {
            if (!TryCreateClient(out var client))
            {
                return;
            }

            var result = await client.GetStatusAsync(PollingPath);
            ApplyBackendResult(result, "Approval status refreshed.");
        });
    }

    public async Task SubmitTotpAsync()
    {
        await RunAsync(async () =>
        {
            if (!TryCreateClient(out var client))
            {
                return;
            }

            var result = await client.SubmitTotpAsync(SessionId, TotpCode.Trim());
            TotpCode = string.Empty;
            ApplyBackendResult(result, "TOTP fallback submitted.");
        });
    }

    public Task ClearTotpAsync()
    {
        TotpCode = string.Empty;
        LastMessage = "TOTP code cleared.";
        return Task.CompletedTask;
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or HttpRequestException or TaskCanceledException)
        {
            LastMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryCreateClient(out IReferenceBackendClient client)
    {
        client = null!;
        if (!Uri.TryCreate(ReferenceBackendBaseUrl.Trim(), UriKind.Absolute, out var backendBaseUrl)
            || (backendBaseUrl.Scheme != Uri.UriSchemeHttp && backendBaseUrl.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(backendBaseUrl.UserInfo))
        {
            LastMessage = "Reference backend URL must be an absolute HTTP(S) URL without credentials.";
            return false;
        }

        client = _clientFactory.Create(backendBaseUrl);
        return true;
    }

    private void ApplyBackendResult(
        ReferenceBackendResult<ReferenceApprovalSession> result,
        string successMessage)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            LastMessage = result.StatusCode is null
                ? result.ErrorMessage ?? "Reference backend request failed."
                : $"{result.ErrorMessage} Status code: {result.StatusCode}.";
            return;
        }

        ApplySession(result.Value);
        LastMessage = successMessage;
    }

    private void ApplySession(ReferenceApprovalSession session)
    {
        SessionId = session.SessionId;
        PollingPath = session.PollingPath;
        ApprovalStatus = session.Status.ToString();
        IsCommitted = session.IsCommitted;
        LatencySummary = FormatLatency(session.Latency);
        RaiseCommandStateChanged();
    }

    private DemoSettings CreateSettings()
    {
        return new DemoSettings
        {
            ReferenceBackendBaseUrl = ReferenceBackendBaseUrl.Trim(),
            ExternalUserId = ExternalUserId.Trim(),
            OperationDisplayName = NormalizeDisplayName(),
            Dt1520BaseUrl = Dt1520BaseUrl.Trim(),
            TenantId = TenantId.Trim(),
            ApplicationClientId = ApplicationClientId.Trim(),
            ClientId = ClientId.Trim(),
            ClientSecret = ClientSecret,
            CallbackSigningSecret = CallbackSigningSecret,
            CallbackUrl = CallbackUrl.Trim(),
            Scope = Scope.Trim(),
        };
    }

    private void ApplySettings(DemoSettings settings)
    {
        ReferenceBackendBaseUrl = settings.ReferenceBackendBaseUrl;
        ExternalUserId = settings.ExternalUserId;
        OperationDisplayName = settings.OperationDisplayName;
        Dt1520BaseUrl = settings.Dt1520BaseUrl;
        TenantId = settings.TenantId;
        ApplicationClientId = settings.ApplicationClientId;
        ClientId = settings.ClientId;
        ClientSecret = settings.ClientSecret;
        CallbackSigningSecret = settings.CallbackSigningSecret;
        CallbackUrl = settings.CallbackUrl;
        Scope = settings.Scope;
        TotpCode = string.Empty;
    }
}
