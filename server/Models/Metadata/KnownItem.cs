namespace server.Models.Metadata
{
    [Table("known_items")]
    public sealed class KnownItem
    {
        [Key]
        public uint Id { get; set; }

        public uint Quality { get; set; }
    }
}