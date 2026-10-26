using server.Models.Admin;
using server.Models.Entity;

namespace server.Services.Telemetry;

// Scoped
public sealed class ClientVersionService(AppDbContext appDbContext, TimeProvider timeProvider)
{
    /// <summary>同一设备两次落库的最小间隔（秒），版本没变时按这个节流。</summary>
    public const long TrackIntervalSeconds = 3600;

    /// <summary>记录一台设备当前使用的客户端版本：新设备建行，版本变化或超过节流窗口才写库。</summary>
    public async ValueTask TrackAsync(string deviceId, string version)
    {
        ClientVersion? tracked = await appDbContext.ClientVersions.SingleOrDefaultAsync(v => v.DeviceId == deviceId).ConfigureAwait(false);
        long now = timeProvider.GetUnixTimeSeconds();

        if (tracked is null)
        {
            appDbContext.ClientVersions.Add(new ClientVersion
            {
                DeviceId = deviceId,
                Version = version,
                FirstSeenAt = now,
                LastSeenAt = now,
            });

            await appDbContext.SaveChangesAsync().ConfigureAwait(false);
            return;
        }

        if (tracked.Version == version && now - tracked.LastSeenAt < TrackIntervalSeconds)
        {
            return;
        }

        tracked.Version = version;
        tracked.LastSeenAt = now;
        await appDbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>按版本汇总在册设备数，设备数相同的按版本号降序。</summary>
    public async Task<ClientVersionStatistics> GetStatisticsAsync()
    {
        List<ClientVersionCount> counts = await appDbContext.ClientVersions
            .AsNoTracking()
            .GroupBy(version => version.Version)
            .Select(group => new ClientVersionCount
            {
                Version = group.Key,
                DeviceCount = group.Count(),
                FirstSeenAt = group.Min(item => item.FirstSeenAt),
                LastSeenAt = group.Max(item => item.LastSeenAt),
            })
            .ToListAsync()
            .ConfigureAwait(false);

        return new ClientVersionStatistics
        {
            TotalDevices = counts.Sum(item => item.DeviceCount),
            Versions = [.. counts
                .OrderByDescending(item => item.DeviceCount)
                .ThenByDescending(item => Version.Parse(item.Version))],
        };
    }
}
