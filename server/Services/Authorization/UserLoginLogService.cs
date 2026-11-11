using server.Models.Passport;

namespace server.Services.Authorization;

// Scoped
public sealed class UserLoginLogService(AppDbContext appDbContext, TimeProvider timeProvider, ILogger<UserLoginLogService> logger)
{
    private const int _maxUserNameLength = 256;

    public async Task RecordAsync(HttpContext httpContext, DeviceInfo? deviceInfo, string method, string? userName, bool success, string? failureKey, int? userId = null)
    {
        try
        {
            string? attemptedName = string.IsNullOrWhiteSpace(userName) ? null : Truncate(userName.Trim(), _maxUserNameLength);

            if (success && userId is null && attemptedName is not null)
            {
                string normalizedUserName = attemptedName.ToUpperInvariant();
                userId = await appDbContext.Users.AsNoTracking()
                    .Where(u => u.NormalizedUserName == normalizedUserName)
                    .Select(u => (int?)u.Id)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
            }

            appDbContext.UserLoginLogs.Add(new()
            {
                UserName = attemptedName,
                UserId = success ? userId : null,
                Method = method,
                Success = success,
                FailureKey = success ? null : failureKey,
                Ip = httpContext.GetClientIp(),
                DeviceId = Truncate(deviceInfo?.DeviceId, 64),
                DeviceName = Truncate(deviceInfo?.DeviceName?.Trim(), 128),
                OperatingSystem = Truncate(deviceInfo?.OperatingSystem?.Trim(), 64),
                LoginAt = timeProvider.GetUtcNow(),
            });

            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 记日志失败绝不能影响登录本身
            logger.LogWarning(ex, "Failed to write user login log");
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }
}
