using System.Collections.Concurrent;

namespace server.Controllers.Filter;

// Singleton：把胡桃客户端上报的版本记进 client_versions，供后台版本统计
public sealed class ClientVersionTrackFilter(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ClientVersionTrackFilter> logger) : IAsyncActionFilter
{
    // 进程内的节流表，避免每个请求都查库；容量上界就是安装设备数
    private readonly ConcurrentDictionary<string, (string Version, long WrittenAt)> _lastTracked = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next().ConfigureAwait(false);

        if (!context.HttpContext.TryGetClientVersion(out Version? version) || !context.HttpContext.TryGetDeviceId(out string? deviceId))
        {
            return;
        }

        string reported = version.ToString();
        long now = timeProvider.GetUnixTimeSeconds();

        // 只有版本没变才节流；变了要立刻落库，否则后台要等一个节流窗口才能看到升级
        if (_lastTracked.TryGetValue(deviceId, out (string Version, long WrittenAt) tracked)
            && tracked.Version == reported
            && now - tracked.WrittenAt < ClientVersionService.TrackIntervalSeconds)
        {
            return;
        }

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ClientVersionService>().TrackAsync(deviceId, reported).ConfigureAwait(false);
            _lastTracked[deviceId] = (reported, now);
        }
        catch (Exception ex)
        {
            // 版本统计失败绝不能影响业务请求
            logger.LogWarning(ex, "Failed to track client version for device {DeviceId}", deviceId);
        }
    }
}
