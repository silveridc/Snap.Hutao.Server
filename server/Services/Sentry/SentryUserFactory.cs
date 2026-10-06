using Microsoft.Extensions.Primitives;
using Sentry;

namespace server.Services.Sentry
{
    public sealed class SentryUserFactory(IHttpContextAccessor httpContextAccessor) : ISentryUserFactory
    {
        public SentryUser? Create()
        {
            return httpContextAccessor.HttpContext is { } context ? Create(context) : default;
        }

        private static SentryUser? Create(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue("X-Real-IP", out StringValues realIps))
            {
                return default;
            }

            string realIp = realIps.ToString();

            _ = context.Request.Headers.TryGetValue("x-hutao-device-id", out StringValues deviceIds);
            string deviceId = deviceIds.ToString();

            return new()
            {
                Id = deviceId,
                IpAddress = realIp,
            };
        }
    }
}