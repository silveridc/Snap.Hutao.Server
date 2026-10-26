namespace server.Models.Admin;

public sealed class ClientVersionStatistics
{
    public int TotalDevices { get; set; }

    public List<ClientVersionCount> Versions { get; set; } = [];
}

public sealed class ClientVersionCount
{
    public string Version { get; set; } = default!;

    public int DeviceCount { get; set; }

    public long FirstSeenAt { get; set; }

    public long LastSeenAt { get; set; }
}
