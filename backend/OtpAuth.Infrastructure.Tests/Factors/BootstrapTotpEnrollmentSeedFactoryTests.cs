using OtpAuth.Infrastructure.Factors;
using OtpAuth.Infrastructure.Integrations;
using Xunit;

namespace OtpAuth.Infrastructure.Tests.Factors;

public sealed class BootstrapTotpEnrollmentSeedFactoryTests
{
    [Fact]
    public void Create_UsesExplicitEnvironmentValues()
    {
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID", "user-seed-001");
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64", Convert.ToBase64String("ABCDEFGHIJKLMNOPQRSTUVWX12345678"u8.ToArray()));
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_USERNAME", "seed.user");
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_TENANT_ID", "6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb");
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_APPLICATION_CLIENT_ID", "f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4");

        try
        {
            var material = new BootstrapTotpEnrollmentSeedFactory().Create(new BootstrapOAuthOptions());

            Assert.Equal("user-seed-001", material.ExternalUserId);
            Assert.Equal("seed.user", material.Username);
            Assert.Equal(6, material.Digits);
            Assert.Equal(30, material.PeriodSeconds);
            Assert.Equal("SHA1", material.Algorithm);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID", null);
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64", null);
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_USERNAME", null);
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_TENANT_ID", null);
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_APPLICATION_CLIENT_ID", null);
        }
    }

    [Fact]
    public void Create_FallsBackToBootstrapClientScope_WhenTenantAndAppAreOmitted()
    {
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID", "user-seed-002");
        Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64", Convert.ToBase64String("ZYXWVUTSRQPONMLKJIHGFEDCBA987654"u8.ToArray()));

        try
        {
            var material = new BootstrapTotpEnrollmentSeedFactory().Create(
                new BootstrapOAuthOptions
                {
                    Clients =
                    [
                        new BootstrapOAuthClientOptions
                        {
                            ClientId = "otpauth-crm",
                            TenantId = Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"),
                            ApplicationClientId = Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"),
                            ClientSecretEnvVarName = "IGNORED",
                            AllowedScopes = ["challenges:read"],
                        },
                    ],
                });

            Assert.Equal(Guid.Parse("6e8c2d4d-7eb0-4cb9-b582-5ff0afc6d3fb"), material.TenantId);
            Assert.Equal(Guid.Parse("f7e5f55c-5ef8-4b84-aa33-d2dcac91c9d4"), material.ApplicationClientId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_EXTERNAL_USER_ID", null);
            Environment.SetEnvironmentVariable("OTPAUTH_BOOTSTRAP_TOTP_SECRET_BASE64", null);
        }
    }
}
