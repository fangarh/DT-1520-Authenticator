using System.Xml.Linq;

namespace Dt1520.Authenticator.Desktop.Tests;

public sealed class DesktopPackageScaffoldTests
{
    [Fact]
    public void DesktopProjectHasRequiredPackageScaffold()
    {
        var project = SdkScaffold.Project("src", "Dt1520.Authenticator.Desktop", "Dt1520.Authenticator.Desktop.csproj");
        var properties = SdkScaffold.Properties(project);
        var marker = SdkScaffold.ReadText("src", "Dt1520.Authenticator.Desktop", "PackageAssemblyMarker.cs");

        Assert.Equal("net8.0", properties["TargetFramework"]);
        Assert.Equal("true", properties["IsPackable"]);
        Assert.Equal("true", properties["GenerateDocumentationFile"]);
        Assert.Equal("Dt1520.Authenticator.Desktop", properties["PackageId"]);
        Assert.Equal("Dt1520.Authenticator.Desktop", properties["RootNamespace"]);
        Assert.Contains("internal static class PackageAssemblyMarker", marker, StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopPackageReadmeRejectsSecretBearingDirectIntegration()
    {
        var readme = SdkScaffold.ReadText("src", "Dt1520.Authenticator.Desktop", "README.md");

        Assert.Contains("Desktop App -> Integrator Backend -> DT-1520 Authenticator", readme, StringComparison.Ordinal);
        Assert.Contains("must not contain", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Polling example", readme, StringComparison.Ordinal);
        Assert.Contains("relative and stay under the configured integrator backend origin", readme, StringComparison.Ordinal);
        Assert.Contains("non-approval", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret=", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", readme, StringComparison.Ordinal);
    }
}

internal static class SdkScaffold
{
    public static XElement Project(params string[] parts)
    {
        var path = Path.Combine([Root(), .. parts]);
        Assert.True(File.Exists(path), $"Expected project to exist: {path}");

        var project = XElement.Load(path);
        Assert.Equal("Microsoft.NET.Sdk", project.Attribute("Sdk")?.Value);

        return project;
    }

    public static Dictionary<string, string> Properties(XElement project)
    {
        return project
            .Descendants("PropertyGroup")
            .Elements()
            .GroupBy(element => element.Name.LocalName)
            .ToDictionary(group => group.Key, group => group.Last().Value);
    }

    public static string ReadText(params string[] parts)
    {
        var path = Path.Combine([Root(), .. parts]);
        Assert.True(File.Exists(path), $"Expected file to exist: {path}");

        return File.ReadAllText(path);
    }

    public static string FilePath(params string[] parts)
    {
        var path = System.IO.Path.Combine([Root(), .. parts]);
        Assert.True(File.Exists(path), $"Expected file to exist: {path}");

        return path;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dt1520.Authenticator.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate lib workspace root.");
    }
}
