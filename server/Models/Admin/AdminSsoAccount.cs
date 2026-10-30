namespace server.Models.Admin;

public sealed class AdminSsoAccount
{
    public int Id
    {
        get; set;
    }

    public string UserName { get; set; } = default!;

    public string? Email
    {
        get; set;
    }

    public string Provider { get; set; } = default!;

    public string Subject { get; set; } = default!;

    public bool IsBootstrap
    {
        get; set;
    }
}
