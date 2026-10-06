namespace server.Models.Upload
{
    public sealed class HutaoUploadLog
    {
        public string Id { get; set; } = default!;

        public long Time { get; set; }

        public string Info { get; set; } = default!;
    }
}