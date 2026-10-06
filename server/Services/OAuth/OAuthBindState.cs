using server.Models.Passport;

namespace server.Services.OAuth
{
    public sealed class OAuthBindState(string deviceId, string? deviceName, string? deviceOs, string callbackUri)
    {
        public OAuthBindState(int userId, string deviceId, string? deviceName, string? deviceOs, string callbackUri)
            : this(deviceId, deviceName, deviceOs, callbackUri)
        {
            UserId = userId;
        }

        public int UserId { get; set; } = -1;

        public DeviceInfo DeviceInfo { get; set; } = new(deviceId, deviceName, deviceOs);

        public string CallbackUri { get; set; } = callbackUri;
    }
}