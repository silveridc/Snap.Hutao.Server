CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
    CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `gacha_events` (
    `Version` varchar(64) NOT NULL,
    `Name` varchar(255) NOT NULL,
    `Locale` varchar(16) NOT NULL,
    `Order` int unsigned NOT NULL,
    `From` datetime NOT NULL,
    `To` datetime NOT NULL,
    `Type` int NOT NULL,
    `UpOrangeItem1` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem2` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem3` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem4` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem5` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem6` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem7` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem8` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem9` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem10` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem11` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem12` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem13` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem14` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem15` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem16` int unsigned NOT NULL DEFAULT 0,
    `UpOrangeItem17` int unsigned NOT NULL DEFAULT 0,
    `UpPurpleItem1` int unsigned NOT NULL DEFAULT 0,
    `UpPurpleItem2` int unsigned NOT NULL DEFAULT 0,
    `UpPurpleItem3` int unsigned NOT NULL DEFAULT 0,
    `UpPurpleItem4` int unsigned NOT NULL DEFAULT 0,
    `UpPurpleItem5` int unsigned NOT NULL DEFAULT 0,
    PRIMARY KEY (`Version`, `Name`, `Locale`, `Order`),
    INDEX `IX_gacha_events_Type` (`Type`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `known_items` (
    `Id` int unsigned NOT NULL,
    `Quality` int unsigned NOT NULL,
    PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

-- Yae 成就注入配置（/client/AchievementFieldId_{version}.json 的数据源）
-- MetadataDbContext 没有迁移，线上按本文件手工建表；列类型与 EF 生成的 CreateTable 一致
CREATE TABLE IF NOT EXISTS `yae_achievement_metadata` (
    `Version` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
    `ChineseHash` int unsigned NOT NULL,
    `OverseaHash` int unsigned NOT NULL,
    `StoreCmdId` int unsigned NOT NULL,
    `AchievementCmdId` int unsigned NOT NULL,
    `ItemCount` int unsigned NOT NULL,
    `GroupCount` int unsigned NOT NULL,
    `Payload` longtext CHARACTER SET utf8mb4 NOT NULL,
    `SyncedAt` datetime NOT NULL,
    PRIMARY KEY (`Version`)
) CHARACTER SET=utf8mb4;

-- metadata 原文档表 + i18n 覆盖表（/metadata/Genshin/{locale}/{name}.json 的数据源）
-- 与 yae_achievement_metadata 一样：MetadataDbContext 没有迁移，线上按本文件手工建表
-- Doc 是去掉 CR 之后的 JSON 原文，Hash 是它的 XXH64（大写 16 位十六进制），对外直接吐 Doc 本体
-- 名字列不做大小写敏感：EF 在测试里建的表也是默认 collation，开发与线上行为保持一致
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
-- 投影口径由 upd-test 的三个探针实测决定，写在下面每张表的注释里，改设计前先重跑探针。

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
-- （实测靠数值命中推断会得出「Achievement.FinishReward.Id 指向 FurnitureMake」这种假阳性，
--   而 Avatar 的 Talents/Skills/Inherents/Costumes 压根没有对应的 metadata 分类）
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

-- 已在库里建过 metadata_document 的话，这条单独执行（新建库不用管，上面的 CREATE 里已经有了）；
-- MySQL 不支持 ADD INDEX IF NOT EXISTS，重复执行会报 1061（Duplicate key name），可以安全忽略
-- ALTER TABLE `metadata_document` ADD INDEX `IX_metadata_document_Name_Locale` (`Name`, `Locale`);
