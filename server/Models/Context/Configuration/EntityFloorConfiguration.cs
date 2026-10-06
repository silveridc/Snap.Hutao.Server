using Microsoft.EntityFrameworkCore.Metadata.Builders;
using server.Models.Entity.SpiralAbyss;
using server.Models.Upload;

namespace server.Models.Context.Configuration
{
    /// <summary>
    /// 层信息配置
    /// </summary>
    public class EntityFloorConfiguration : IEntityTypeConfiguration<EntityFloor>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<EntityFloor> builder)
        {
            builder.Property(e => e.Levels)
                .HasColumnType("longtext")
                .HasConversion<JsonTextValueConverter<List<SimpleLevel>>>();
        }
    }
}