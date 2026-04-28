namespace Dt1520.Authenticator.Client.Tests;

public sealed class PrereleaseHandoffTests
{
    [Fact]
    public void PrereleaseHandoffDocumentsPackageGateAndReferenceStandApis()
    {
        var handoff = RepoText("lib", "PRERELEASE-HANDOFF.md");

        Assert.Contains("0.1.0-alpha.1", handoff, StringComparison.Ordinal);
        Assert.Contains("dotnet pack .\\Dt1520.Authenticator.slnx", handoff, StringComparison.Ordinal);
        Assert.Contains("Dt1520AuthenticatorClient.CreateChallengeAsync", handoff, StringComparison.Ordinal);
        Assert.Contains("Dt1520AuthenticatorClient.VerifyTotpAsync", handoff, StringComparison.Ordinal);
        Assert.Contains("Dt1520AuthenticatorCallbackValidator", handoff, StringComparison.Ordinal);
        Assert.Contains("DesktopApprovalPoller", handoff, StringComparison.Ordinal);
        Assert.Contains("original raw HTTP request body bytes", handoff, StringComparison.Ordinal);
        Assert.Contains("Desktop App -> Reference Backend -> DT-1520 Authenticator -> Android App", handoff, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret=", handoff, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", handoff, StringComparison.Ordinal);
        Assert.DoesNotContain("pushToken=", handoff, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceStandReadmePreservesBackendSecretBoundary()
    {
        var readme = RepoText("rdb_stand", "README.md");

        Assert.Contains("Desktop App -> Reference Backend -> DT-1520 Authenticator -> Android App", readme, StringComparison.Ordinal);
        Assert.Contains("CreateChallengeAsync", readme, StringComparison.Ordinal);
        Assert.Contains("VerifyTotpAsync", readme, StringComparison.Ordinal);
        Assert.Contains("DesktopApprovalPoller", readme, StringComparison.Ordinal);
        Assert.Contains("call only the reference backend", readme, StringComparison.Ordinal);
        Assert.Contains("never hold DT-1520 integration credentials", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret=", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("pushToken=", readme, StringComparison.Ordinal);
    }

    private static string RepoText(params string[] parts)
    {
        var path = Path.Combine([RepoRoot(), .. parts]);
        Assert.True(File.Exists(path), $"Expected file to exist: {path}");

        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "OTP"))
                && Directory.Exists(Path.Combine(directory.FullName, "lib"))
                && Directory.Exists(Path.Combine(directory.FullName, "rdb_stand")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
