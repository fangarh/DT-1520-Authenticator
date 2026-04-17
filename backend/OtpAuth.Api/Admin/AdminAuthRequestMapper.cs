using OtpAuth.Application.Administration;

namespace OtpAuth.Api.Admin;

public static class AdminAuthRequestMapper
{
    public static AdminLoginRequest Map(AdminLoginHttpRequest request, string? remoteAddress)
    {
        return new AdminLoginRequest
        {
            Username = request.Username,
            Password = request.Password,
            RemoteAddress = remoteAddress,
        };
    }

    public static AdminSessionHttpResponse MapResponse(AdminAuthenticatedUser user)
    {
        return new AdminSessionHttpResponse
        {
            AdminUserId = user.AdminUserId,
            Username = user.Username,
            Permissions = user.Permissions,
        };
    }
}
