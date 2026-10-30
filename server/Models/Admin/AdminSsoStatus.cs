namespace server.Models.Admin;

public sealed class AdminSsoStatus
{
    public bool Enabled
    {
        get; set;
    }

    public string? DisplayName
    {
        get; set;
    }
}
