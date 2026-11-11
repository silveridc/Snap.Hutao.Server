namespace server.Models.Admin;

public sealed class BatchUidRequest
{
    public List<string> Uids { get; set; } = [];

    public string? Reason
    {
        get; set;
    }
}
