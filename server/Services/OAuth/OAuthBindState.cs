using server.Models.Passport;

namespace server.Services.OAuth
{
    public sealed class OAuthBindState(string deviceId, string? deviceName, string? deviceOs, string callbackUri)
    {
        /// <summary>
        /// 反序列化专用：System.Text.Json 要求被选中的构造函数的每个参数都能绑定到属性，
        /// 主构造函数那套（deviceId/deviceName/deviceOs）无法匹配 <see cref="DeviceInfo"/>，
        /// 不显式指定构造函数时 /OAuth/Callback/GitHub 会直接抛异常。
        /// </summary>
        [JsonConstructor]
        public OAuthBindState(int userId, DeviceInfo deviceInfo, string callbackUri)
            : this(deviceInfo.DeviceId, deviceInfo.DeviceName, deviceInfo.OperatingSystem, callbackUri)
        {
            UserId = userId;
        }

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