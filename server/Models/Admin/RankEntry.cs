namespace server.Models.Admin;

public sealed class RankEntry
{
    public string Uid { get; set; } = default!;

    public double Value
    {
        get; set;
    }
}
