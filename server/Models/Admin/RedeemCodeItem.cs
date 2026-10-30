namespace server.Models.Admin;

public sealed class RedeemCodeItem
{
    public uint Id
    {
        get; set;
    }

    public string Code { get; set; } = default!;

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

    public string Creator { get; set; } = default!;

    public DateTimeOffset CreateTime
    {
        get; set;
    }

    public DateTimeOffset ExpireTime
    {
        get; set;
    }

    public uint TimesAllowed
    {
        get; set;
    }

    public int UsedCount
    {
        get; set;
    }
}
