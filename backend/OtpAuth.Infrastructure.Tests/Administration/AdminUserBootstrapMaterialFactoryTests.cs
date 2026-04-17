using OtpAuth.Application.Administration;
using OtpAuth.Infrastructure.Administration;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Administration;

public sealed class AdminUserBootstrapMaterialFactoryTests
{
    private readonly AdminUserBootstrapMaterialFactory _factory = new(new Pbkdf2AdminPasswordHasher());

    [Fact]
    public void Create_NormalizesUsername_HashesPassword_AndDeduplicatesPermissions()
    {
        var material = _factory.Create(
            " operator ",
            "super-secret-123",
            [AdminPermissions.EnrollmentsWrite, AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsRead]);

        Assert.Equal("operator", material.Username);
        Assert.Equal("OPERATOR", material.NormalizedUsername);
        Assert.Equal(
            [AdminPermissions.EnrollmentsRead, AdminPermissions.EnrollmentsWrite],
            material.Permissions);
        Assert.DoesNotContain("super-secret-123", material.PasswordHash, StringComparison.Ordinal);
        Assert.True(new Pbkdf2AdminPasswordHasher().Verify("super-secret-123", material.PasswordHash));
    }

    [Fact]
    public void Create_Throws_WhenPasswordIsTooShort()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _factory.Create(
            "operator",
            "short",
            [AdminPermissions.EnrollmentsRead]));

        Assert.Equal("Admin password must be at least 12 characters long.", exception.Message);
    }

    [Fact]
    public void Create_Throws_WhenPermissionIsUnknown()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _factory.Create(
            "operator",
            "super-secret-123",
            ["unknown.permission"]));

        Assert.Equal("Unsupported admin permission 'unknown.permission'.", exception.Message);
    }

    [Fact]
    public void Create_Throws_WhenPermissionsAreMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _factory.Create(
            "operator",
            "super-secret-123",
            []));

        Assert.Equal("At least one admin permission must be provided.", exception.Message);
    }
}
