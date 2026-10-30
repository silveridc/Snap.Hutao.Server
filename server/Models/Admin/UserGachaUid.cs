namespace server.Models.Admin;

public sealed class UserGachaUid
{
    public string Uid { get; set; } = default!;

    public int ItemCount
    {
        get; set;
    }
}
