using Microsoft.EntityFrameworkCore.Design;

namespace server.Models.Context
{
    internal sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            DbContextOptionsBuilder<AppDbContext> builder = new();
            string connectionString = configuration.GetConnectionString("PrimaryMysql8")!;
            // 设计时（生成迁移）不连接数据库，固定版本避免 AutoDetect 需要在线数据库
            builder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 32)));
            return new AppDbContext(builder.Options);
        }
    }
}