using server.Models.Passport;

namespace server.Controllers
{
    internal static class DeviceExtension
    {
        private const string HutaoDeviceIdHeader = "x-hutao-device-id";
        private const string HutaoDeviceNameHeader = "x-hutao-device-name";
        private const string HutaoDeviceOsHeader = "x-hutao-device-os";

        /// <summary>
        /// 从控制器请求头中读取设备信息。
        /// </summary>
        /// <param name="controller">当前控制器。</param>
        /// <returns>设备信息。</returns>
        public static DeviceInfo GetDeviceInfo(this ControllerBase controller)
        {
            return controller.HttpContext.GetDeviceInfo();
        }

        /// <summary>
        /// 从操作执行上下文的请求头中读取设备信息。
        /// </summary>
        /// <param name="context">操作执行上下文。</param>
        /// <returns>设备信息。</returns>
        public static DeviceInfo GetDeviceInfo(this ActionExecutingContext context)
        {
            return context.HttpContext.GetDeviceInfo();
        }

        /// <summary>
        /// 从 HTTP 请求头中读取设备信息。
        /// </summary>
        /// <param name="context">HTTP 上下文。</param>
        /// <returns>设备信息。</returns>
        public static DeviceInfo GetDeviceInfo(this HttpContext context)
        {
            string? deviceId = context.Request.Headers[HutaoDeviceIdHeader];
            string? deviceName = context.Request.Headers[HutaoDeviceNameHeader];
            string? deviceOs = context.Request.Headers[HutaoDeviceOsHeader];

            ArgumentNullException.ThrowIfNull(deviceId);

            return new(deviceId, deviceName, deviceOs);
        }
    }
}