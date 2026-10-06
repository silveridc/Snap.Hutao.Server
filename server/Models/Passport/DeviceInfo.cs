namespace server.Models.Passport
{
    public class DeviceInfo(string deviceId, string? deviceName, string? operatingSystem)
    {
        public string DeviceId { get; set; } = deviceId;

        public string? DeviceName { get; set; } = deviceName;

        public string? OperatingSystem { get; set; } = operatingSystem;
    }
}