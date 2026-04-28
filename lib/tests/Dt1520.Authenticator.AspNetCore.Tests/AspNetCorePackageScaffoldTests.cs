using System.Xml.Linq;

namespace Dt1520.Authenticator.AspNetCore.Tests;

public sealed class AspNetCorePackageScaffoldTests
{
    [Fact]
    public void AspNetCoreProjectHasRequiredPackageScaffold()
    {
        var project = SdkScaffold.Project("src", "Dt1520.Authenticator.AspNetCore", "Dt1520.Authenticator.AspNetCore.csproj");
        var properties = SdkScaffold.Properties(project);
        var marker = SdkScaffold.ReadText("src", "Dt1520.Authenticator.AspNetCore", "PackageAssemblyMarker.cs");

        Assert.Equal("net8.0", properties["TargetFramework"]);
        Assert.Equal("true", properties["IsPackable"]);
        Assert.Equal("true", properties["GenerateDocumentationFile"]);
        Assert.Equal("Dt1520.Authenticator.AspNetCore", properties["PackageId"]);
        Assert.Equal("Dt1520.Authenticator.AspNetCore", properties["RootNamespace"]);
        Assert.Contains("ASP.NET Core", properties["Description"], StringComparison.Ordinal);
        Assert.Contains("internal static class PackageAssemblyMarker", marker, StringComparison.Ordinal);
    }

    [Fact]
    public void PackageReadmeStatesServerSideSecretBoundary()
    {
        var readme = SdkScaffold.ReadText("src", "Dt1520.Authenticator.AspNetCore", "README.md");

        Assert.Contains("server-side configuration", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Minimal API protected operation flow", readme, StringComparison.Ordinal);
        Assert.Contains("Validate the DT-1520 callback signature", readme, StringComparison.Ordinal);
        Assert.Contains("Commit the pending draft exactly once only after", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("example-secret", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password=", readme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("client_secret=", readme, StringComparison.OrdinalIgnoreCase);
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
