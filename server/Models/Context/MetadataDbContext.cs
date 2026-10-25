using server.Models.Metadata;

namespace server.Models.Context;

public sealed class MetadataDbContext(DbContextOptions<MetadataDbContext> options) : DbContext(options)
{
    public DbSet<GachaEventInfo> GachaEvents { get; set; }

    public DbSet<KnownItem> KnownItems { get; set; }

    public DbSet<YaeAchievementMetadata> YaeAchievementMetadata { get; set; }

    public DbSet<MetadataDocument> MetadataDocuments { get; set; }

    public DbSet<MetadataTranslation> MetadataTranslations { get; set; }

    public DbSet<MetadataKind> MetadataKinds { get; set; }

    public DbSet<MetadataObject> MetadataObjects { get; set; }

    public DbSet<MetadataObjectI18n> MetadataObjectI18ns { get; set; }

    public DbSet<MetadataObjectLayout> MetadataObjectLayouts { get; set; }

    public DbSet<MetadataObjectRef> MetadataObjectRefs { get; set; }

    /// <summary>
    /// 提列用 MySQL 的 STORED 生成列，而不是让投影顺手写这几列：这样列上的值和 <see cref="MetadataObject.Doc"/>
    /// 里的值不可能对不上。表达式与 update_metadata.sql 逐字一致，测试里 CreateTablesAsync 建出的 schema
    /// 跟线上手工建表就是一样的。`NULL ON ERROR` 不是保险丝而是必需项——被抽成 i18n 的叶子在骨架里是字符串占位符，
    /// 少这一句就会把它们统统算成 0。
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetadataKind>(entity => entity.Property(property => property.RootShape).HasConversion<string>().HasMaxLength(16));

        modelBuilder.Entity<MetadataObject>(entity =>
        {
            entity.Property(property => property.Quality).HasComputedColumnSql(Unsigned("$.Quality"), stored: true);
            entity.Property(property => property.Rank).HasComputedColumnSql(Unsigned("$.Rank"), stored: true);
            entity.Property(property => property.RankLevel).HasComputedColumnSql(Unsigned("$.RankLevel"), stored: true);
            entity.Property(property => property.Type).HasComputedColumnSql(Signed("$.Type"), stored: true);
            entity.Property(property => property.ItemType).HasComputedColumnSql(Unsigned("$.ItemType"), stored: true);
            entity.Property(property => property.MaterialType).HasComputedColumnSql(Unsigned("$.MaterialType"), stored: true);
            entity.Property(property => property.WeaponType).HasComputedColumnSql(Unsigned("$.WeaponType"), stored: true);
            entity.Property(property => property.SetId).HasComputedColumnSql(Unsigned("$.SetId"), stored: true);
            entity.Property(property => property.PromoteId).HasComputedColumnSql(Unsigned("$.PromoteId"), stored: true);
            entity.Property(property => property.ChapterId).HasComputedColumnSql(Unsigned("$.ChapterId"), stored: true);
            entity.Property(property => property.Sort).HasComputedColumnSql(Signed("$.Sort"), stored: true);
        });
    }

    private static string Unsigned(string path)
    {
        return $"JSON_VALUE(`Doc`, '{path}' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)";
    }

    private static string Signed(string path)
    {
        return $"JSON_VALUE(`Doc`, '{path}' RETURNING SIGNED INTEGER NULL ON EMPTY NULL ON ERROR)";
    }
}
