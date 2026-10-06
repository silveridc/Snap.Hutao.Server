using server.Models.Metadata;

namespace server.Models.Context
{
    public sealed class MetadataDbContext(DbContextOptions<MetadataDbContext> options) : DbContext(options)
    {
        public DbSet<GachaEventInfo> GachaEvents { get; set; }

        public DbSet<KnownItem> KnownItems { get; set; }
    }
}