using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

// 元数据三表原先由 MetadataDbContext 在启动时 CreateTablesAsync 建出（连接串与主库相同），
// 存量库里可能已经存在，所以这里用 IF NOT EXISTS，升级时不会撞表。
/// <inheritdoc />
public partial class MergeMetadataTables : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS `gacha_events` (
                `Version` varchar(255) NOT NULL,
                `Name` varchar(255) NOT NULL,
                `Locale` varchar(255) NOT NULL,
                `Order` int unsigned NOT NULL,
                `From` datetime NOT NULL,
                `To` datetime NOT NULL,
                `UpOrangeItem1` int unsigned NOT NULL,
                `UpOrangeItem2` int unsigned NOT NULL,
                `UpOrangeItem3` int unsigned NOT NULL,
                `UpOrangeItem4` int unsigned NOT NULL,
                `UpOrangeItem5` int unsigned NOT NULL,
                `UpOrangeItem6` int unsigned NOT NULL,
                `UpOrangeItem7` int unsigned NOT NULL,
                `UpOrangeItem8` int unsigned NOT NULL,
                `UpOrangeItem9` int unsigned NOT NULL,
                `UpOrangeItem10` int unsigned NOT NULL,
                `UpOrangeItem11` int unsigned NOT NULL,
                `UpOrangeItem12` int unsigned NOT NULL,
                `UpOrangeItem13` int unsigned NOT NULL,
                `UpOrangeItem14` int unsigned NOT NULL,
                `UpOrangeItem15` int unsigned NOT NULL,
                `UpOrangeItem16` int unsigned NOT NULL,
                `UpOrangeItem17` int unsigned NOT NULL,
                `UpPurpleItem1` int unsigned NOT NULL,
                `UpPurpleItem2` int unsigned NOT NULL,
                `UpPurpleItem3` int unsigned NOT NULL,
                `UpPurpleItem4` int unsigned NOT NULL,
                `UpPurpleItem5` int unsigned NOT NULL,
                `Type` int NOT NULL,
                PRIMARY KEY (`Version`, `Name`, `Locale`, `Order`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS `known_items` (
                `Id` int unsigned NOT NULL AUTO_INCREMENT,
                `Quality` int unsigned NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS `yae_achievement_metadata` (
                `Version` varchar(16) NOT NULL,
                `ChineseHash` int unsigned NOT NULL,
                `OverseaHash` int unsigned NOT NULL,
                `StoreCmdId` int unsigned NOT NULL,
                `AchievementCmdId` int unsigned NOT NULL,
                `ItemCount` int unsigned NOT NULL,
                `GroupCount` int unsigned NOT NULL,
                `Payload` longtext NOT NULL,
                `SyncedAt` datetime NOT NULL,
                PRIMARY KEY (`Version`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `yae_achievement_metadata`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `known_items`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `gacha_events`;");
    }
}
