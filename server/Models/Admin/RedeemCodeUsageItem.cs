namespace server.Models.Admin;

/// <summary>某张兑换码的一条使用记录.</summary>
public sealed class RedeemCodeUsageItem
{
    public int UserId
    {
        get; set;
    }

    public string? UserName
    {
        get; set;
    }

    public DateTimeOffset UseTime
    {
        get; set;
    }
}
