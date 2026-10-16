using Microsoft.EntityFrameworkCore.Design;

namespace server
{
    public class MetadataDbContextFactory : IDesignTimeDbContextFactory<MetadataDbContext>
    {
        public MetadataDbContext CreateDbContext(string[] args)
        {
            DbContextOptionsBuilder<MetadataDbContext> optionsBuilder = new();

            string connectionString = "Server=localhost;Database=snaphutao;User=root;Password=root;";
            MySqlServerVersion serverVersion = new(new Version(8, 0, 30));

            optionsBuilder.UseMySql(connectionString, serverVersion);

            return new MetadataDbContext(optionsBuilder.Options);
        }
    }
}