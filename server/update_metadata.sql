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
