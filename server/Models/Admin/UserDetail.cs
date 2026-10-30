namespace server.Models.Admin;

public sealed class UserDetail
{
    public int Id
    {
        get; set;
    }

    public string? UserName
    {
        get; set;
    }

    public bool IsMaintainer
    {
        get; set;
    }

    public bool IsLicensedDeveloper
    {
        get; set;
    }

    public long GachaLogExpireAt
    {
        get; set;
    }

    public long CdnExpireAt
    {
        get; set;
    }

    public int RedeemUsedCount
    {
        get; set;
    }

    public List<UserGachaUid> GachaUids { get; set; } = [];
}
