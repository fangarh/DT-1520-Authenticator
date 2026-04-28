using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dt1520.Authenticator.Desktop;
using Dt1520.Authenticator.ReferenceDesktop;

var options = DesktopShellOptions.FromEnvironment();
var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
jsonOptions.Converters.Add(new JsonStringEnumConverter());
using var httpClient = new HttpClient
{
    BaseAddress = options.BackendBaseUrl,
    Timeout = Timeout.InfiniteTimeSpan,
};

Console.WriteLine("DT-1520 reference desktop shell");
Console.Write("External user id: ");
var externalUserId = Console.ReadLine();

if (string.IsNullOrWhiteSpace(externalUserId))
{
    Console.Error.WriteLine("External user id is required.");
    return 1;
}

var startResponse = await httpClient.PostAsJsonAsync(
    "/api/reference/operations",
    new StartProtectedOperationRequest(externalUserId.Trim(), "Reference desktop operation"),
    jsonOptions);

if (!startResponse.IsSuccessStatusCode)
{
    Console.Error.WriteLine($"Backend rejected operation start: {(int)startResponse.StatusCode}");
    return 1;
}

var session = await startResponse.Content.ReadFromJsonAsync<DesktopApprovalSession>(jsonOptions);
if (session is null)
{
    Console.Error.WriteLine("Backend returned an invalid approval session.");
    return 1;
}

Console.WriteLine("Waiting for push approval. Press Enter to use online TOTP fallback.");
using var cts = new CancellationTokenSource();
var poller = new DesktopApprovalPoller(httpClient, new DesktopApprovalPollingOptions
{
    BackendBaseUrl = options.BackendBaseUrl,
    PollInterval = TimeSpan.FromSeconds(2),
    Timeout = TimeSpan.FromMinutes(2),
});

var pollingTask = poller.PollUntilCompletedAsync(session, cts.Token);
var inputTask = Task.Run(Console.ReadLine);
var completed = await Task.WhenAny(pollingTask, inputTask);

if (completed == inputTask)
{
    await cts.CancelAsync();
    Console.Write("TOTP code: ");
    var code = Console.ReadLine();
    var totpResponse = await httpClient.PostAsJsonAsync(
        $"/api/reference/operations/{Uri.EscapeDataString(session.SessionId)}/totp",
        new VerifyTotpFallbackRequest(code ?? string.Empty),
        jsonOptions);

    Console.WriteLine(totpResponse.IsSuccessStatusCode
        ? "TOTP fallback submitted. Check backend status for terminal result."
        : $"TOTP fallback rejected: {(int)totpResponse.StatusCode}");
    return totpResponse.IsSuccessStatusCode ? 0 : 1;
}

var outcome = await pollingTask;
Console.WriteLine($"Approval outcome: {outcome.Kind}");
return outcome.Kind == DesktopApprovalOutcomeKind.Approved ? 0 : 1;
