namespace server.Models.Passport;

public sealed class PassportResetLinkState
{
    public string UserName { get; set; } = default!;

    public int ExpireInSeconds
    {
        get; set;
    }
}
