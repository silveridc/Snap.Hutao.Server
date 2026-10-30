namespace server.Models.Admin;

public sealed class LicenseApplicationItem
{
    public long PrimaryId
    {
        get; set;
    }

    public int UserId
    {
        get; set;
    }

    public string? UserName
    {
        get; set;
    }

    public string ProjectUrl { get; set; } = default!;

    public string ApprovalCode { get; set; } = default!;

    public bool IsApproved
    {
        get; set;
    }
}
