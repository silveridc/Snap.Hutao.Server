namespace server.Models.Admin;

public sealed class RedeemCodeUpdateRequest
{
    public uint Id
    {
        get; set;
    }

    public uint Type
    {
        get; set;
    }

    public uint ServiceType
    {
        get; set;
    }

    public int Value
    {
        get; set;
    }

    public string Description { get; set; } = default!;

    public DateTimeOffset ExpireTime
    {
        get; set;
    }

    public uint TimesAllowed
    {
        get; set;
    }
}
