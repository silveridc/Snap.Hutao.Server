using System.Collections.Concurrent;

namespace server.Filter;

// Singleton
public sealed class ClientVersionTrackFilter(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<ClientVersionTrackFilter> logger) : IAsyncActionFilter
{
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
            logger.LogWarning(ex, "Failed to track client version for device {DeviceId}", deviceId);
        }
    }
}
