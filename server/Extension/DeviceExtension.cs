using server.Models.Passport;

namespace server.Extension;

internal static class DeviceExtension
{
    private const string _hutaoDeviceIdHeader = "x-hutao-device-id";
    private const string _hutaoDeviceNameHeader = "x-hutao-device-name";
    private const string _hutaoDeviceOsHeader = "x-hutao-device-os";
    private const int _maxDeviceIdLength = 32;

    /// <summary>
    /// Reads the device information from the controller request headers.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <returns>The device information.</returns>
    public static DeviceInfo GetDeviceInfo(this ControllerBase controller)
    {
        return controller.HttpContext.GetDeviceInfo();
    }

    /// <summary>
    /// Reads the device information from the action executing context request headers.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <returns>The device information.</returns>
    public static DeviceInfo GetDeviceInfo(this ActionExecutingContext context)
    {
        return context.HttpContext.GetDeviceInfo();
    }

    /// <summary>
    /// Reads the device information from the HTTP request headers.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The device information.</returns>
    public static DeviceInfo GetDeviceInfo(this HttpContext context)
    {
        string? deviceId = context.Request.Headers[_hutaoDeviceIdHeader];
        string? deviceName = context.Request.Headers[_hutaoDeviceNameHeader];
        string? deviceOs = context.Request.Headers[_hutaoDeviceOsHeader];

        ArgumentNullException.ThrowIfNull(deviceId);

        return new(deviceId, deviceName, deviceOs);
    }

    /// <summary>
    /// Reads the device ID reported by the Hutao client from the HTTP request headers.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="deviceId">The device ID; at most 32 characters.</param>
    /// <returns>Whether the request headers carry a valid device ID.</returns>
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
