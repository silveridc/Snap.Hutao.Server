namespace server.Models.Admin;

public sealed class BanItem
{
    public string Uid { get; set; } = default!;

    public string? Reason
    {
        get; set;
    }

    public long BanTime
    {
        get; set;
    }
}
