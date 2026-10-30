namespace server.Models.Admin;

public sealed class BatchIdRequest
{
    public List<uint> Ids { get; set; } = [];
}
