using server.Models.Passport;

namespace server.Extension;

internal static class DeviceExtension
{
    private const string _hutaoDeviceIdHeader = "x-hutao-device-id";
    private const string _hutaoDeviceNameHeader = "x-hutao-device-name";
    private const string _hutaoDeviceOsHeader = "x-hutao-device-os";
    private const int _maxDeviceIdLength = 32;

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
        string? deviceId = context.Request.Headers[_hutaoDeviceIdHeader];
        string? deviceName = context.Request.Headers[_hutaoDeviceNameHeader];
        string? deviceOs = context.Request.Headers[_hutaoDeviceOsHeader];

        ArgumentNullException.ThrowIfNull(deviceId);

        return new(deviceId, deviceName, deviceOs);
    }

    /// <summary>
    /// 从 HTTP 请求头中读取胡桃客户端上报的设备 id。
    /// </summary>
    /// <param name="context">HTTP 上下文。</param>
    /// <param name="deviceId">设备 id，长度上限为 32。</param>
    /// <returns>请求头中是否携带合法的设备 id。</returns>
    public static bool TryGetDeviceId(this HttpContext context, [NotNullWhen(true)] out string? deviceId)
    {
        deviceId = context.Request.Headers[_hutaoDeviceIdHeader].ToString();

        if (deviceId.Length is 0 || deviceId.Length > _maxDeviceIdLength)
        {
            return false;
        }

        return true;
    }
}
