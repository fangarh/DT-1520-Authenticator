using OtpAuth.Application.Administration;

namespace OtpAuth.Infrastructure.Administration;

internal sealed record AdminTenantDirectoryTenantPersistenceModel
{
    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required string Status { get; init; }

    public required int ApplicationCount { get; init; }

    public required int IntegrationClientCount { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

internal sealed record AdminTenantDirectoryApplicationPersistenceModel
{
    public required Guid ApplicationClientId { get; init; }

    public required Guid TenantId { get; init; }

    public required string DisplayName { get; init; }

    public string? Slug { get; init; }

    public required string Status { get; init; }

    public required int IntegrationClientCount { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset? UpdatedUtc { get; init; }
}

internal static class AdminTenantDirectoryDataMapper
{
    public static AdminTenantDirectoryTenantView ToTenantView(AdminTenantDirectoryTenantPersistenceModel model)
    {
        return new AdminTenantDirectoryTenantView
        {
            TenantId = model.TenantId,
            DisplayName = model.DisplayName,
            Slug = model.Slug,
            Status = FromPersistenceStatus(model.Status),
            ApplicationCount = model.ApplicationCount,
            IntegrationClientCount = model.IntegrationClientCount,
            CreatedUtc = model.CreatedUtc,
            UpdatedUtc = model.UpdatedUtc,
        };
    }

    public static AdminTenantDirectoryApplicationView ToApplicationView(AdminTenantDirectoryApplicationPersistenceModel model)
    {
        return new AdminTenantDirectoryApplicationView
        {
            ApplicationClientId = model.ApplicationClientId,
            TenantId = model.TenantId,
            DisplayName = model.DisplayName,
            Slug = model.Slug,
            Status = FromPersistenceStatus(model.Status),
            IntegrationClientCount = model.IntegrationClientCount,
            CreatedUtc = model.CreatedUtc,
            UpdatedUtc = model.UpdatedUtc,
        };
    }

    public static string ToPersistenceStatus(AdminTenantDirectoryStatus status)
    {
        return status switch
        {
            AdminTenantDirectoryStatus.Active => "active",
            AdminTenantDirectoryStatus.Disabled => "disabled",
            AdminTenantDirectoryStatus.Archived => "archived",
            AdminTenantDirectoryStatus.Test => "test",
            _ => throw new InvalidOperationException($"Unsupported tenant directory status '{status}'."),
        };
    }

    public static AdminTenantDirectoryStatus FromPersistenceStatus(string status)
    {
        return status switch
        {
            "active" => AdminTenantDirectoryStatus.Active,
            "disabled" => AdminTenantDirectoryStatus.Disabled,
            "archived" => AdminTenantDirectoryStatus.Archived,
            "test" => AdminTenantDirectoryStatus.Test,
            _ => throw new InvalidOperationException($"Unsupported tenant directory status '{status}'."),
        };
    }
}
