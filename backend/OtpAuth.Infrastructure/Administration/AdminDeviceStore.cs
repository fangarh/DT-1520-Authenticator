using Dapper;
using Npgsql;
using OtpAuth.Application.Administration;
using OtpAuth.Domain.Devices;

namespace OtpAuth.Infrastructure.Administration;

public sealed class PostgresAdminDeviceStore : IAdminDeviceStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAdminDeviceStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyCollection<AdminUserDeviceView>> ListByExternalUserAsync(
        AdminUserDeviceListRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var models = await connection.QueryAsync<AdminDevicePersistenceModel>(new CommandDefinition(
            """
            select
                id as DeviceId,
                platform as Platform,
                status as Status,
                (push_token is not null and btrim(push_token) <> '') as IsPushCapable,
                activated_utc as ActivatedUtc,
                last_seen_utc as LastSeenUtc,
                revoked_utc as RevokedUtc,
                blocked_utc as BlockedUtc
            from auth.devices
            where tenant_id = @TenantId
              and external_user_id = @ExternalUserId
              and status in (@ActiveStatus, @RevokedStatus, @BlockedStatus)
            order by
                case
                    when status = @ActiveStatus then 0
                    when status = @BlockedStatus then 1
                    when status = @RevokedStatus then 2
                    else 3
                end,
                coalesce(last_seen_utc, blocked_utc, revoked_utc, activated_utc, created_utc) desc,
                id desc;
            """,
            new
            {
                request.TenantId,
                request.ExternalUserId,
                ActiveStatus = DeviceStatus.Active,
                RevokedStatus = DeviceStatus.Revoked,
                BlockedStatus = DeviceStatus.Blocked,
            },
            cancellationToken: cancellationToken));

        return models
            .Select(AdminDeviceDataMapper.ToDomainModel)
            .ToArray();
    }
}
