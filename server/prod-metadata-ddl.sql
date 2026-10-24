-- 生产库 snaphutao 补建 metadata 表（修 /Service/Metadata/* 的 500：Table 'metadata_translation' doesn't exist）
--
-- 内容逐字摘自仓库里的 server/update_metadata.sql 第 66-192 行，没有改写任何一个字段；
-- 去掉的只有 UTF-8 BOM（首字节 ef bb bf，直接管道进 mysql 会让第一条语句报 1064）
-- 和与本次故障无关的 __EFMigrationsHistory / gacha_events / known_items / yae_achievement_metadata 四段。
--
-- 执行：mysql -u<user> -p snaphutao < prod-metadata-ddl.sql
-- 库名走命令行，不写 USE；全部 CREATE TABLE IF NOT EXISTS，重复执行安全。

-- ============================================================
-- 唯一真源（对外逐字节服务就靠这两张）
-- ============================================================

CREATE TABLE IF NOT EXISTS `metadata_document` (
    `Locale` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Doc` longtext CHARACTER SET utf8mb4 NOT NULL,
    `Hash` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `Size` int unsigned NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Locale`, `Name`),
    -- 投影按 Name 取一个文档在所有 locale 上的正文（散文件分类一次一个文件），
    -- 只有 (Locale, Name) 主键时这一句会全表扫 343 MB：实测 Avatar 因此从 ~40 s 涨到 410 s
    INDEX `IX_metadata_document_Name_Locale` (`Name`, `Locale`)
) CHARACTER SET=utf8mb4;

-- 这条索引不是可选的。表如果上次已经建出来但少了它，上面的 CREATE 会被 IF NOT EXISTS 整个跳过，
-- 所以这里按 information_schema 判断后补一次；已存在就执行一条空 SELECT，重复跑不会撞 1061。
SET @ix := (SELECT COUNT(*) FROM information_schema.STATISTICS
             WHERE TABLE_SCHEMA = DATABASE()
               AND TABLE_NAME = 'metadata_document'
               AND INDEX_NAME = 'IX_metadata_document_Name_Locale');
SET @ddl := IF(@ix = 0,
    'ALTER TABLE `metadata_document` ADD INDEX `IX_metadata_document_Name_Locale` (`Name`, `Locale`)',
    'SELECT ''index already present'' AS noop');
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- 只存人工改过的字符串叶子，未改动的文档一行都不占；删除覆盖时用 OriginalValue 还原
CREATE TABLE IF NOT EXISTS `metadata_translation` (
    `Locale` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Pointer` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Value` longtext CHARACTER SET utf8mb4 NOT NULL,
    `OriginalValue` longtext CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Locale`, `Name`, `Pointer`)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- 派生读模型（对外 API 用）
-- ============================================================
-- 上面两张表是「逐字节服务」的唯一真源：/metadata/Genshin/{locale}/{name}.json 直接吐 metadata_document.Doc。
-- 下面 5 张表全部由 MetadataProjectionService 从 metadata_document 投影出来，随时可以 TRUNCATE 重建，
-- 服务一个字节都不依赖它们；它们存在的理由是把 28,340 个对象身份（CHS 单语言 26,538 个）摊成能过滤、能反查的关系表。

-- 分类注册表：身份键怎么取、根形态是什么、这次投影有没有被后续的翻译改动弄脏
CREATE TABLE IF NOT EXISTS `metadata_kind` (
    `Kind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `DocumentName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `RootShape` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `GameIdPath` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `SubIdPath` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `ObjectCount` int unsigned NOT NULL,
    `LocaleCount` smallint unsigned NOT NULL,
    `RawCount` int unsigned NOT NULL,
    `ProjectedAt` datetime NOT NULL,
    `StaleSince` datetime NULL,
    PRIMARY KEY (`Kind`),
    INDEX `IX_metadata_kind_DocumentName` (`DocumentName`)
) CHARACTER SET=utf8mb4;

-- 对象骨架：一行 = 一个游戏对象（全 locale 共用一份结构），随 locale 变的叶子在骨架里写成占位符
-- ⚠ 下面的生成列用了 JSON_VALUE(... NULL ON EMPTY NULL ON ERROR)，只有较新的 MySQL 支持；
--   如果这段报 1064/3142，前面 4 张表已经建成、后台页面就能用了，先别硬改这段的写法——
--   EF 模型里这些列声明成了 HasComputedColumnSql(stored: true)，口径必须逐字一致，改完投影自检会对不上。
CREATE TABLE IF NOT EXISTS `metadata_object` (
    `Kind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `GameId` bigint unsigned NOT NULL,
    `SubId` int unsigned NOT NULL,
    `SourceLocale` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
    `Ordinal` int unsigned NOT NULL,
    `Doc` mediumtext CHARACTER SET utf8mb4 NOT NULL,
    -- 以下都是 Doc 上的生成列，只做过滤/排序，值永远以 Doc 为准。
    -- NULL ON ERROR 是必须的：占位符是字符串，能转数的 JSON_VALUE 会静默给出错的值，转不了数的正好落成 NULL。
    `Quality` int unsigned AS (JSON_VALUE(`Doc`, '$.Quality' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `Rank` int unsigned AS (JSON_VALUE(`Doc`, '$.Rank' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `RankLevel` int unsigned AS (JSON_VALUE(`Doc`, '$.RankLevel' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `Type` bigint AS (JSON_VALUE(`Doc`, '$.Type' RETURNING SIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `ItemType` int unsigned AS (JSON_VALUE(`Doc`, '$.ItemType' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `MaterialType` int unsigned AS (JSON_VALUE(`Doc`, '$.MaterialType' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `WeaponType` int unsigned AS (JSON_VALUE(`Doc`, '$.WeaponType' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `SetId` bigint unsigned AS (JSON_VALUE(`Doc`, '$.SetId' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `PromoteId` bigint unsigned AS (JSON_VALUE(`Doc`, '$.PromoteId' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `ChapterId` bigint unsigned AS (JSON_VALUE(`Doc`, '$.ChapterId' RETURNING UNSIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `Sort` bigint AS (JSON_VALUE(`Doc`, '$.Sort' RETURNING SIGNED INTEGER NULL ON EMPTY NULL ON ERROR)) STORED,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Kind`, `GameId`, `SubId`),
    INDEX `IX_metadata_object_Kind_Ordinal` (`Kind`, `Ordinal`),
    INDEX `IX_metadata_object_Kind_Quality` (`Kind`, `Quality`),
    INDEX `IX_metadata_object_Kind_Rank` (`Kind`, `Rank`),
    INDEX `IX_metadata_object_Kind_RankLevel` (`Kind`, `RankLevel`),
    INDEX `IX_metadata_object_Kind_Type` (`Kind`, `Type`),
    INDEX `IX_metadata_object_Kind_ItemType` (`Kind`, `ItemType`),
    INDEX `IX_metadata_object_Kind_MaterialType` (`Kind`, `MaterialType`),
    INDEX `IX_metadata_object_Kind_WeaponType` (`Kind`, `WeaponType`),
    INDEX `IX_metadata_object_Kind_SetId` (`Kind`, `SetId`),
    INDEX `IX_metadata_object_Kind_PromoteId` (`Kind`, `PromoteId`),
    INDEX `IX_metadata_object_Kind_ChapterId` (`Kind`, `ChapterId`),
    INDEX `IX_metadata_object_Kind_Sort` (`Kind`, `Sort`)
) CHARACTER SET=utf8mb4;

-- locale 叶子：一行 = 一个对象的一个随语言变化的叶子（占位符指向它），CHS 也存，装配路径只有一条
-- `Value` 存的是 JSON token 本身而不是裸字符串：字符串带引号、内部按 JSON 转义（实测 Material/EN 有 `"\"Sun\""` 这种名字），
-- 所以外部按文本比对要走 JSON_UNQUOTE(`Value`)，直接 = 裸关键词会漏也会假命中
CREATE TABLE IF NOT EXISTS `metadata_object_i18n` (
    `Kind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `GameId` bigint unsigned NOT NULL,
    `SubId` int unsigned NOT NULL,
    `Field` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Locale` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
    `Value` mediumtext CHARACTER SET utf8mb4 NOT NULL,
    `UpdatedAt` datetime NOT NULL,
    PRIMARY KEY (`Kind`, `GameId`, `SubId`, `Field`, `Locale`),
    INDEX `IX_metadata_object_i18n_Kind_Locale_Field` (`Kind`, `Locale`, `Field`)
) CHARACTER SET=utf8mb4;

-- 每个 locale 的文档成员与顺序：CHS 不是超集（实测 CHT 的 MainQuest 有 4412 个对象、CHS 只有 2696 个），
-- 所以「哪些对象、按什么顺序出现」必须按 locale 各存一份，不能从结构表推断
CREATE TABLE IF NOT EXISTS `metadata_object_layout` (
    `Locale` varchar(8) CHARACTER SET utf8mb4 NOT NULL,
    `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `Position` int unsigned NOT NULL,
    `Kind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `GameId` bigint unsigned NOT NULL,
    `SubId` int unsigned NOT NULL,
    PRIMARY KEY (`Locale`, `Name`, `Position`),
    INDEX `IX_metadata_object_layout_Kind_Id` (`Kind`, `GameId`, `SubId`, `Locale`)
) CHARACTER SET=utf8mb4;

-- 跨分类引用边：Role 是代码里白名单化的字段语义（MetadataRefRegistry.cs），不做数值猜靶
CREATE TABLE IF NOT EXISTS `metadata_object_ref` (
    `Kind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `GameId` bigint unsigned NOT NULL,
    `SubId` int unsigned NOT NULL,
    `Role` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
    `Ordinal` int unsigned NOT NULL,
    `RefKind` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    `RefId` bigint unsigned NOT NULL,
    PRIMARY KEY (`Kind`, `GameId`, `SubId`, `Role`, `Ordinal`),
    INDEX `IX_metadata_object_ref_Target` (`RefKind`, `RefId`, `Role`)
) CHARACTER SET=utf8mb4;

-- ============================================================
-- 自检（紧接着打印，不用另外敲）
-- ============================================================

-- 期望 7 行：metadata_document / _translation / _kind / _object / _object_i18n / _object_layout / _object_ref
-- TABLE_ROWS 是 InnoDB 估算值，建完表是 0 或 1 都正常，导入后才看行数
SELECT TABLE_NAME, TABLE_ROWS
  FROM information_schema.TABLES
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME LIKE 'metadata%'
 ORDER BY TABLE_NAME;

-- 期望 2（STATISTICS 每列一行：复合索引 (Name, Locale) 两列各计一行）。
-- 0 就是那条要命的索引没落地，投影会退化成全表扫
SELECT COUNT(*) AS ix_name_locale_ok
  FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = 'metadata_document'
   AND INDEX_NAME = 'IX_metadata_document_Name_Locale';

-- 期望 27（STATISTICS 每列一行：PRIMARY 3 列 + 12 个二级索引各 2 列）。缺了说明生成列表述被 MySQL 拒了，投影那步会失败
SELECT COUNT(*) AS object_indexes_ok
  FROM information_schema.STATISTICS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = 'metadata_object';
