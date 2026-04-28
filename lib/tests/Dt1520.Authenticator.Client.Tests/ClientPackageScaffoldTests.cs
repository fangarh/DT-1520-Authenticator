using System.Xml.Linq;

namespace Dt1520.Authenticator.Client.Tests;

public sealed class ClientPackageScaffoldTests
{
    [Fact]
    public void ClientProjectHasRequiredPackageScaffold()
    {
        var project = SdkScaffold.Project("src", "Dt1520.Authenticator.Client", "Dt1520.Authenticator.Client.csproj");
        var properties = SdkScaffold.Properties(project);
        var marker = SdkScaffold.ReadText("src", "Dt1520.Authenticator.Client", "PackageAssemblyMarker.cs");

        Assert.Equal("net8.0", properties["TargetFramework"]);
        Assert.Equal("true", properties["IsPackable"]);
        Assert.Equal("true", properties["GenerateDocumentationFile"]);
        Assert.Equal("Dt1520.Authenticator.Client", properties["PackageId"]);
        Assert.Equal("Dt1520.Authenticator.Client", properties["RootNamespace"]);
        Assert.DoesNotContain("AspNetCore", project.Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("internal static class PackageAssemblyMarker", marker, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedBuildSettingsEnableStrictPackableLibraries()
    {
        var props = SdkScaffold.Properties(SdkScaffold.File("Directory.Build.props"));
        var targets = SdkScaffold.Properties(SdkScaffold.File("Directory.Build.targets"));

        Assert.Equal("enable", props["Nullable"]);
        Assert.Equal("true", props["TreatWarningsAsErrors"]);
        Assert.Equal("true", props["Deterministic"]);
        Assert.Equal("0.1.0-alpha.1", props["Version"]);
        Assert.Equal("MIT", props["PackageLicenseExpression"]);
        Assert.Equal("true", targets["GenerateDocumentationFile"]);
        Assert.Equal("README.md", targets["PackageReadmeFile"]);
    }

    [Fact]
    public void ClientReadmeDocumentsGettingStartedFallbackAndSecretBoundaries()
    {
        var readme = SdkScaffold.ReadText("src", "Dt1520.Authenticator.Client", "README.md");

        Assert.Contains("Basic backend usage", readme, StringComparison.Ordinal);
        Assert.Contains("VerifyTotpAsync", readme, StringComparison.Ordinal);
        Assert.Contains("Commit the protected business operation only after", readme, StringComparison.Ordinal);
        Assert.Contains("Callback mismatch", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret=", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void SampleBackendFlowDocumentsApprovalBeforeCommit()
    {
        var sample = SdkScaffold.ReadText("samples", "aspnetcore-protected-operation", "README.md");

        Assert.Contains("Start a protected operation", sample, StringComparison.Ordinal);
        Assert.Contains("Receive DT-1520 callback", sample, StringComparison.Ordinal);
        Assert.Contains("Online TOTP fallback", sample, StringComparison.Ordinal);
        Assert.Contains("committed exactly once only after `approved`", sample, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret=", sample, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pushToken=", sample, StringComparison.Ordinal);
    }
}

internal static class SdkScaffold
{
    public static XElement File(params string[] parts)
    {
        var path = Path.Combine([Root(), .. parts]);
        Assert.True(System.IO.File.Exists(path), $"Expected file to exist: {path}");

        return XElement.Load(path);
    }

    public static XElement Project(params string[] parts)
    {
        var project = File(parts);
        Assert.Equal("Microsoft.NET.Sdk", project.Attribute("Sdk")?.Value);

        return project;
    }

    public static string ReadText(params string[] parts)
    {
        var path = Path.Combine([Root(), .. parts]);
        Assert.True(System.IO.File.Exists(path), $"Expected file to exist: {path}");

        return System.IO.File.ReadAllText(path);
    }

    public static Dictionary<string, string> Properties(XElement project)
    {
        return project
            .Descendants("PropertyGroup")
            .Elements()
            .GroupBy(element => element.Name.LocalName)
            .ToDictionary(group => group.Key, group => group.Last().Value);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "Dt1520.Authenticator.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate lib workspace root.");
    }
}
