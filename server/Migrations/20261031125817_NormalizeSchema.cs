using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

/// <inheritdoc />
public partial class NormalizeSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_admin_accounts_AspNetUsers_UserId",
            table: "admin_accounts");

        migrationBuilder.DropForeignKey(
            name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
            table: "AspNetRoleClaims");

        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUserClaims_AspNetUsers_UserId",
            table: "AspNetUserClaims");

        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUserLogins_AspNetUsers_UserId",
            table: "AspNetUserLogins");

        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
            table: "AspNetUserRoles");

        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUserRoles_AspNetUsers_UserId",
            table: "AspNetUserRoles");

        migrationBuilder.DropForeignKey(
            name: "FK_AspNetUserTokens_AspNetUsers_UserId",
            table: "AspNetUserTokens");

        migrationBuilder.DropForeignKey(
            name: "FK_avatars_records_RecordId",
            table: "avatars");

        migrationBuilder.DropForeignKey(
            name: "FK_damage_ranks_spiral_abysses_SpiralAbyssId",
            table: "damage_ranks");

        migrationBuilder.DropForeignKey(
            name: "FK_gacha_items_AspNetUsers_UserId",
            table: "gacha_items");

        migrationBuilder.DropForeignKey(
            name: "FK_hutao_log_items_hutao_logs_LogId",
            table: "hutao_log_items");

        migrationBuilder.DropForeignKey(
            name: "FK_license_application_records_AspNetUsers_UserId",
            table: "license_application_records");

        migrationBuilder.DropForeignKey(
            name: "FK_OAuthBindIdentities_AspNetUsers_UserId",
            table: "OAuthBindIdentities");

        migrationBuilder.DropForeignKey(
            name: "FK_redeem_code_use_items_AspNetUsers_UsedBy",
            table: "redeem_code_use_items");

        migrationBuilder.DropForeignKey(
            name: "FK_redeem_code_use_items_redeem_codes_RedeemCodeId",
            table: "redeem_code_use_items");

        migrationBuilder.DropForeignKey(
            name: "FK_RefreshTokens_AspNetUsers_UserId",
            table: "RefreshTokens");

        migrationBuilder.DropForeignKey(
            name: "FK_role_combat_avatars_role_combat_records_RecordId",
            table: "role_combat_avatars");

        migrationBuilder.DropForeignKey(
            name: "FK_spiral_abysses_records_RecordId",
            table: "spiral_abysses");

        migrationBuilder.DropForeignKey(
            name: "FK_spiral_abysses_floors_spiral_abysses_SpiralAbyssId",
            table: "spiral_abysses_floors");

        migrationBuilder.DropForeignKey(
            name: "FK_take_damage_ranks_spiral_abysses_SpiralAbyssId",
            table: "take_damage_ranks");

        migrationBuilder.DropTable(
            name: "achievements");

        migrationBuilder.DropTable(
            name: "passport_verifications");

        migrationBuilder.DropTable(
            name: "request_statistics");

        migrationBuilder.DropTable(
            name: "achievement_archives");

        migrationBuilder.DropPrimaryKey(
            name: "PK_RefreshTokens",
            table: "RefreshTokens");

        migrationBuilder.DropPrimaryKey(
            name: "PK_OAuthBindIdentities",
            table: "OAuthBindIdentities");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetUserTokens",
            table: "AspNetUserTokens");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetUsers",
            table: "AspNetUsers");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetUserRoles",
            table: "AspNetUserRoles");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetUserLogins",
            table: "AspNetUserLogins");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetUserClaims",
            table: "AspNetUserClaims");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetRoles",
            table: "AspNetRoles");

        migrationBuilder.DropPrimaryKey(
            name: "PK_AspNetRoleClaims",
            table: "AspNetRoleClaims");

        migrationBuilder.RenameTable(
            name: "RefreshTokens",
            newName: "refresh_tokens");

        migrationBuilder.RenameTable(
            name: "OAuthBindIdentities",
            newName: "oauth_bind_identities");

        migrationBuilder.RenameTable(
            name: "AspNetUserTokens",
            newName: "asp_net_user_tokens");

        migrationBuilder.RenameTable(
            name: "AspNetUsers",
            newName: "asp_net_users");

        migrationBuilder.RenameTable(
            name: "AspNetUserRoles",
            newName: "asp_net_user_roles");

        migrationBuilder.RenameTable(
            name: "AspNetUserLogins",
            newName: "asp_net_user_logins");

        migrationBuilder.RenameTable(
            name: "AspNetUserClaims",
            newName: "asp_net_user_claims");

        migrationBuilder.RenameTable(
            name: "AspNetRoles",
            newName: "asp_net_roles");

        migrationBuilder.RenameTable(
            name: "AspNetRoleClaims",
            newName: "asp_net_role_claims");

        migrationBuilder.RenameColumn(
            name: "Payload",
            table: "yae_achievement_metadata",
            newName: "payload");

        migrationBuilder.RenameColumn(
            name: "Version",
            table: "yae_achievement_metadata",
            newName: "version");

        migrationBuilder.RenameColumn(
            name: "SyncedAt",
            table: "yae_achievement_metadata",
            newName: "synced_at");

        migrationBuilder.RenameColumn(
            name: "StoreCmdId",
            table: "yae_achievement_metadata",
            newName: "store_cmd_id");

        migrationBuilder.RenameColumn(
            name: "OverseaHash",
            table: "yae_achievement_metadata",
            newName: "oversea_hash");

        migrationBuilder.RenameColumn(
            name: "ItemCount",
            table: "yae_achievement_metadata",
            newName: "item_count");

        migrationBuilder.RenameColumn(
            name: "GroupCount",
            table: "yae_achievement_metadata",
            newName: "group_count");

        migrationBuilder.RenameColumn(
            name: "ChineseHash",
            table: "yae_achievement_metadata",
            newName: "chinese_hash");

        migrationBuilder.RenameColumn(
            name: "AchievementCmdId",
            table: "yae_achievement_metadata",
            newName: "achievement_cmd_id");

        migrationBuilder.RenameColumn(
            name: "Success",
            table: "user_login_logs",
            newName: "success");

        migrationBuilder.RenameColumn(
            name: "Method",
            table: "user_login_logs",
            newName: "method");

        migrationBuilder.RenameColumn(
            name: "Ip",
            table: "user_login_logs",
            newName: "ip");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "user_login_logs",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UserName",
            table: "user_login_logs",
            newName: "user_name");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "user_login_logs",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "OperatingSystem",
            table: "user_login_logs",
            newName: "operating_system");

        migrationBuilder.RenameColumn(
            name: "LoginAt",
            table: "user_login_logs",
            newName: "login_at");

        migrationBuilder.RenameColumn(
            name: "FailureKey",
            table: "user_login_logs",
            newName: "failure_key");

        migrationBuilder.RenameColumn(
            name: "DeviceName",
            table: "user_login_logs",
            newName: "device_name");

        migrationBuilder.RenameColumn(
            name: "DeviceId",
            table: "user_login_logs",
            newName: "device_id");

        migrationBuilder.RenameIndex(
            name: "IX_user_login_logs_Ip",
            table: "user_login_logs",
            newName: "ix_user_login_logs_ip");

        migrationBuilder.RenameIndex(
            name: "IX_user_login_logs_UserId",
            table: "user_login_logs",
            newName: "ix_user_login_logs_user_id");

        migrationBuilder.RenameIndex(
            name: "IX_user_login_logs_LoginAt",
            table: "user_login_logs",
            newName: "ix_user_login_logs_login_at");

        migrationBuilder.RenameColumn(
            name: "Value",
            table: "take_damage_ranks",
            newName: "value");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "take_damage_ranks",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "SpiralAbyssId",
            table: "take_damage_ranks",
            newName: "spiral_abyss_id");

        migrationBuilder.RenameColumn(
            name: "AvatarId",
            table: "take_damage_ranks",
            newName: "avatar_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "take_damage_ranks",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_take_damage_ranks_Value",
            table: "take_damage_ranks",
            newName: "ix_take_damage_ranks_value");

        migrationBuilder.RenameIndex(
            name: "IX_take_damage_ranks_SpiralAbyssId",
            table: "take_damage_ranks",
            newName: "ix_take_damage_ranks_spiral_abyss_id");

        migrationBuilder.RenameColumn(
            name: "Value",
            table: "system_settings",
            newName: "value");

        migrationBuilder.RenameColumn(
            name: "Key",
            table: "system_settings",
            newName: "key");

        migrationBuilder.RenameColumn(
            name: "UpdatedBy",
            table: "system_settings",
            newName: "updated_by");

        migrationBuilder.RenameColumn(
            name: "UpdatedAt",
            table: "system_settings",
            newName: "updated_at");

        migrationBuilder.RenameColumn(
            name: "Name",
            table: "spiral_abysses_statistics",
            newName: "name");

        migrationBuilder.RenameColumn(
            name: "Data",
            table: "spiral_abysses_statistics",
            newName: "data");

        migrationBuilder.RenameColumn(
            name: "ScheduleId",
            table: "spiral_abysses_statistics",
            newName: "schedule_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "spiral_abysses_statistics",
            newName: "primary_id");

        migrationBuilder.RenameColumn(
            name: "Star",
            table: "spiral_abysses_floors",
            newName: "star");

        migrationBuilder.RenameColumn(
            name: "Levels",
            table: "spiral_abysses_floors",
            newName: "levels");

        migrationBuilder.RenameColumn(
            name: "Index",
            table: "spiral_abysses_floors",
            newName: "index");

        migrationBuilder.RenameColumn(
            name: "SpiralAbyssId",
            table: "spiral_abysses_floors",
            newName: "spiral_abyss_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "spiral_abysses_floors",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_spiral_abysses_floors_SpiralAbyssId",
            table: "spiral_abysses_floors",
            newName: "ix_spiral_abysses_floors_spiral_abyss_id");

        migrationBuilder.RenameColumn(
            name: "TotalWinTimes",
            table: "spiral_abysses",
            newName: "total_win_times");

        migrationBuilder.RenameColumn(
            name: "TotalBattleTimes",
            table: "spiral_abysses",
            newName: "total_battle_times");

        migrationBuilder.RenameColumn(
            name: "RecordId",
            table: "spiral_abysses",
            newName: "record_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "spiral_abysses",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_spiral_abysses_RecordId",
            table: "spiral_abysses",
            newName: "ix_spiral_abysses_record_id");

        migrationBuilder.RenameColumn(
            name: "Data",
            table: "role_combat_statistics",
            newName: "data");

        migrationBuilder.RenameColumn(
            name: "ScheduleId",
            table: "role_combat_statistics",
            newName: "schedule_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "role_combat_statistics",
            newName: "primary_id");

        migrationBuilder.RenameColumn(
            name: "Uploader",
            table: "role_combat_records",
            newName: "uploader");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "role_combat_records",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "UploadTime",
            table: "role_combat_records",
            newName: "upload_time");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "role_combat_records",
            newName: "primary_id");

        migrationBuilder.RenameColumn(
            name: "RecordId",
            table: "role_combat_avatars",
            newName: "record_id");

        migrationBuilder.RenameColumn(
            name: "AvatarId",
            table: "role_combat_avatars",
            newName: "avatar_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "role_combat_avatars",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_role_combat_avatars_RecordId",
            table: "role_combat_avatars",
            newName: "ix_role_combat_avatars_record_id");

        migrationBuilder.RenameColumn(
            name: "UserName",
            table: "registration_records",
            newName: "user_name");

        migrationBuilder.RenameColumn(
            name: "Value",
            table: "redeem_codes",
            newName: "value");

        migrationBuilder.RenameColumn(
            name: "Type",
            table: "redeem_codes",
            newName: "type");

        migrationBuilder.RenameColumn(
            name: "Description",
            table: "redeem_codes",
            newName: "description");

        migrationBuilder.RenameColumn(
            name: "Creator",
            table: "redeem_codes",
            newName: "creator");

        migrationBuilder.RenameColumn(
            name: "Code",
            table: "redeem_codes",
            newName: "code");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "redeem_codes",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "TimesAllowed",
            table: "redeem_codes",
            newName: "times_allowed");

        migrationBuilder.RenameColumn(
            name: "ServiceType",
            table: "redeem_codes",
            newName: "service_type");

        migrationBuilder.RenameColumn(
            name: "ExpireTime",
            table: "redeem_codes",
            newName: "expire_time");

        migrationBuilder.RenameColumn(
            name: "CreateTime",
            table: "redeem_codes",
            newName: "create_time");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "redeem_code_use_items",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UsedBy",
            table: "redeem_code_use_items",
            newName: "used_by");

        migrationBuilder.RenameColumn(
            name: "UseTime",
            table: "redeem_code_use_items",
            newName: "use_time");

        migrationBuilder.RenameColumn(
            name: "RedeemCodeId",
            table: "redeem_code_use_items",
            newName: "redeem_code_id");

        migrationBuilder.RenameIndex(
            name: "IX_redeem_code_use_items_UsedBy",
            table: "redeem_code_use_items",
            newName: "ix_redeem_code_use_items_used_by");

        migrationBuilder.RenameIndex(
            name: "IX_redeem_code_use_items_RedeemCodeId",
            table: "redeem_code_use_items",
            newName: "ix_redeem_code_use_items_redeem_code_id");

        migrationBuilder.RenameColumn(
            name: "Uploader",
            table: "records",
            newName: "uploader");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "records",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "UploadTime",
            table: "records",
            newName: "upload_time");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "records",
            newName: "primary_id");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "license_application_records",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "ProjectUrl",
            table: "license_application_records",
            newName: "project_url");

        migrationBuilder.RenameColumn(
            name: "IsApproved",
            table: "license_application_records",
            newName: "is_approved");

        migrationBuilder.RenameColumn(
            name: "ApprovalCode",
            table: "license_application_records",
            newName: "approval_code");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "license_application_records",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_license_application_records_UserId",
            table: "license_application_records",
            newName: "ix_license_application_records_user_id");

        migrationBuilder.RenameColumn(
            name: "Quality",
            table: "known_items",
            newName: "quality");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "known_items",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "invalid_gacha_uids",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "Version",
            table: "hutao_releases",
            newName: "version");

        migrationBuilder.RenameColumn(
            name: "Validation",
            table: "hutao_releases",
            newName: "validation");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "hutao_releases",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "ReleaseNotes",
            table: "hutao_releases",
            newName: "release_notes");

        migrationBuilder.RenameColumn(
            name: "PublishedBy",
            table: "hutao_releases",
            newName: "published_by");

        migrationBuilder.RenameColumn(
            name: "PublishedAt",
            table: "hutao_releases",
            newName: "published_at");

        migrationBuilder.RenameColumn(
            name: "MirrorsJson",
            table: "hutao_releases",
            newName: "mirrors_json");

        migrationBuilder.RenameColumn(
            name: "MinimumVersion",
            table: "hutao_releases",
            newName: "minimum_version");

        migrationBuilder.RenameColumn(
            name: "IsActive",
            table: "hutao_releases",
            newName: "is_active");

        migrationBuilder.RenameIndex(
            name: "IX_hutao_releases_Version",
            table: "hutao_releases",
            newName: "ix_hutao_releases_version");

        migrationBuilder.RenameIndex(
            name: "IX_hutao_releases_IsActive",
            table: "hutao_releases",
            newName: "ix_hutao_releases_is_active");

        migrationBuilder.RenameColumn(
            name: "Version",
            table: "hutao_logs",
            newName: "version");

        migrationBuilder.RenameColumn(
            name: "Resolved",
            table: "hutao_logs",
            newName: "resolved");

        migrationBuilder.RenameColumn(
            name: "Info",
            table: "hutao_logs",
            newName: "info");

        migrationBuilder.RenameColumn(
            name: "Count",
            table: "hutao_logs",
            newName: "count");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "hutao_logs",
            newName: "primary_id");

        migrationBuilder.RenameColumn(
            name: "Time",
            table: "hutao_log_items",
            newName: "time");

        migrationBuilder.RenameColumn(
            name: "LogId",
            table: "hutao_log_items",
            newName: "log_id");

        migrationBuilder.RenameColumn(
            name: "DeviceId",
            table: "hutao_log_items",
            newName: "device_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "hutao_log_items",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_hutao_log_items_LogId",
            table: "hutao_log_items",
            newName: "ix_hutao_log_items_log_id");

        migrationBuilder.RenameColumn(
            name: "Name",
            table: "gacha_statistics",
            newName: "name");

        migrationBuilder.RenameColumn(
            name: "Data",
            table: "gacha_statistics",
            newName: "data");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "gacha_statistics",
            newName: "primary_id");

        migrationBuilder.RenameColumn(
            name: "Time",
            table: "gacha_items",
            newName: "time");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "gacha_items",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "gacha_items",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "gacha_items",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "QueryType",
            table: "gacha_items",
            newName: "query_type");

        migrationBuilder.RenameColumn(
            name: "ItemId",
            table: "gacha_items",
            newName: "item_id");

        migrationBuilder.RenameColumn(
            name: "IsTrusted",
            table: "gacha_items",
            newName: "is_trusted");

        migrationBuilder.RenameColumn(
            name: "GachaType",
            table: "gacha_items",
            newName: "gacha_type");

        migrationBuilder.RenameIndex(
            name: "IX_gacha_items_UserId",
            table: "gacha_items",
            newName: "ix_gacha_items_user_id");

        migrationBuilder.RenameColumn(
            name: "Type",
            table: "gacha_events",
            newName: "type");

        migrationBuilder.RenameColumn(
            name: "To",
            table: "gacha_events",
            newName: "to");

        migrationBuilder.RenameColumn(
            name: "From",
            table: "gacha_events",
            newName: "from");

        migrationBuilder.RenameColumn(
            name: "Order",
            table: "gacha_events",
            newName: "order");

        migrationBuilder.RenameColumn(
            name: "Locale",
            table: "gacha_events",
            newName: "locale");

        migrationBuilder.RenameColumn(
            name: "Name",
            table: "gacha_events",
            newName: "name");

        migrationBuilder.RenameColumn(
            name: "Version",
            table: "gacha_events",
            newName: "version");

        migrationBuilder.RenameColumn(
            name: "UpPurpleItem5",
            table: "gacha_events",
            newName: "up_purple_item5");

        migrationBuilder.RenameColumn(
            name: "UpPurpleItem4",
            table: "gacha_events",
            newName: "up_purple_item4");

        migrationBuilder.RenameColumn(
            name: "UpPurpleItem3",
            table: "gacha_events",
            newName: "up_purple_item3");

        migrationBuilder.RenameColumn(
            name: "UpPurpleItem2",
            table: "gacha_events",
            newName: "up_purple_item2");

        migrationBuilder.RenameColumn(
            name: "UpPurpleItem1",
            table: "gacha_events",
            newName: "up_purple_item1");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem9",
            table: "gacha_events",
            newName: "up_orange_item9");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem8",
            table: "gacha_events",
            newName: "up_orange_item8");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem7",
            table: "gacha_events",
            newName: "up_orange_item7");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem6",
            table: "gacha_events",
            newName: "up_orange_item6");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem5",
            table: "gacha_events",
            newName: "up_orange_item5");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem4",
            table: "gacha_events",
            newName: "up_orange_item4");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem3",
            table: "gacha_events",
            newName: "up_orange_item3");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem2",
            table: "gacha_events",
            newName: "up_orange_item2");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem17",
            table: "gacha_events",
            newName: "up_orange_item17");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem16",
            table: "gacha_events",
            newName: "up_orange_item16");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem15",
            table: "gacha_events",
            newName: "up_orange_item15");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem14",
            table: "gacha_events",
            newName: "up_orange_item14");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem13",
            table: "gacha_events",
            newName: "up_orange_item13");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem12",
            table: "gacha_events",
            newName: "up_orange_item12");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem11",
            table: "gacha_events",
            newName: "up_orange_item11");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem10",
            table: "gacha_events",
            newName: "up_orange_item10");

        migrationBuilder.RenameColumn(
            name: "UpOrangeItem1",
            table: "gacha_events",
            newName: "up_orange_item1");

        migrationBuilder.RenameColumn(
            name: "Value",
            table: "damage_ranks",
            newName: "value");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "damage_ranks",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "SpiralAbyssId",
            table: "damage_ranks",
            newName: "spiral_abyss_id");

        migrationBuilder.RenameColumn(
            name: "AvatarId",
            table: "damage_ranks",
            newName: "avatar_id");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "damage_ranks",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_damage_ranks_Value",
            table: "damage_ranks",
            newName: "ix_damage_ranks_value");

        migrationBuilder.RenameIndex(
            name: "IX_damage_ranks_SpiralAbyssId",
            table: "damage_ranks",
            newName: "ix_damage_ranks_spiral_abyss_id");

        migrationBuilder.RenameColumn(
            name: "Version",
            table: "client_versions",
            newName: "version");

        migrationBuilder.RenameColumn(
            name: "LastSeenAt",
            table: "client_versions",
            newName: "last_seen_at");

        migrationBuilder.RenameColumn(
            name: "FirstSeenAt",
            table: "client_versions",
            newName: "first_seen_at");

        migrationBuilder.RenameColumn(
            name: "DeviceId",
            table: "client_versions",
            newName: "device_id");

        migrationBuilder.RenameColumn(
            name: "Reason",
            table: "banned",
            newName: "reason");

        migrationBuilder.RenameColumn(
            name: "Uid",
            table: "banned",
            newName: "uid");

        migrationBuilder.RenameColumn(
            name: "BanTime",
            table: "banned",
            newName: "ban_time");

        migrationBuilder.RenameColumn(
            name: "WeaponId",
            table: "avatars",
            newName: "weapon_id");

        migrationBuilder.RenameColumn(
            name: "ReliquarySet",
            table: "avatars",
            newName: "reliquary_set");

        migrationBuilder.RenameColumn(
            name: "RecordId",
            table: "avatars",
            newName: "record_id");

        migrationBuilder.RenameColumn(
            name: "AvatarId",
            table: "avatars",
            newName: "avatar_id");

        migrationBuilder.RenameColumn(
            name: "ActivedConstellationNumber",
            table: "avatars",
            newName: "actived_constellation_number");

        migrationBuilder.RenameColumn(
            name: "PrimaryId",
            table: "avatars",
            newName: "primary_id");

        migrationBuilder.RenameIndex(
            name: "IX_avatars_RecordId",
            table: "avatars",
            newName: "ix_avatars_record_id");

        migrationBuilder.RenameColumn(
            name: "Title",
            table: "announcements",
            newName: "title");

        migrationBuilder.RenameColumn(
            name: "Severity",
            table: "announcements",
            newName: "severity");

        migrationBuilder.RenameColumn(
            name: "Link",
            table: "announcements",
            newName: "link");

        migrationBuilder.RenameColumn(
            name: "Kind",
            table: "announcements",
            newName: "kind");

        migrationBuilder.RenameColumn(
            name: "Enabled",
            table: "announcements",
            newName: "enabled");

        migrationBuilder.RenameColumn(
            name: "Content",
            table: "announcements",
            newName: "content");

        migrationBuilder.RenameColumn(
            name: "Locale",
            table: "announcements",
            newName: "locale");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "announcements",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "MaxPresentVersion",
            table: "announcements",
            newName: "max_present_version");

        migrationBuilder.RenameColumn(
            name: "LastUpdateTime",
            table: "announcements",
            newName: "last_update_time");

        migrationBuilder.RenameColumn(
            name: "Header",
            table: "allowed_versions",
            newName: "header");

        migrationBuilder.RenameColumn(
            name: "Timestamp",
            table: "admin_audit_logs",
            newName: "timestamp");

        migrationBuilder.RenameColumn(
            name: "Success",
            table: "admin_audit_logs",
            newName: "success");

        migrationBuilder.RenameColumn(
            name: "Ip",
            table: "admin_audit_logs",
            newName: "ip");

        migrationBuilder.RenameColumn(
            name: "Detail",
            table: "admin_audit_logs",
            newName: "detail");

        migrationBuilder.RenameColumn(
            name: "Action",
            table: "admin_audit_logs",
            newName: "action");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "admin_audit_logs",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "AdminUserId",
            table: "admin_audit_logs",
            newName: "admin_user_id");

        migrationBuilder.RenameColumn(
            name: "AdminAccountName",
            table: "admin_audit_logs",
            newName: "admin_account_name");

        migrationBuilder.RenameColumn(
            name: "AdminAccountId",
            table: "admin_audit_logs",
            newName: "admin_account_id");

        migrationBuilder.RenameColumn(
            name: "Subject",
            table: "admin_accounts",
            newName: "subject");

        migrationBuilder.RenameColumn(
            name: "Provider",
            table: "admin_accounts",
            newName: "provider");

        migrationBuilder.RenameColumn(
            name: "Email",
            table: "admin_accounts",
            newName: "email");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "admin_accounts",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UserName",
            table: "admin_accounts",
            newName: "user_name");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "admin_accounts",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "LastLoginAt",
            table: "admin_accounts",
            newName: "last_login_at");

        migrationBuilder.RenameColumn(
            name: "IsEnabled",
            table: "admin_accounts",
            newName: "is_enabled");

        migrationBuilder.RenameColumn(
            name: "IsBootstrap",
            table: "admin_accounts",
            newName: "is_bootstrap");

        migrationBuilder.RenameColumn(
            name: "CreatedAt",
            table: "admin_accounts",
            newName: "created_at");

        migrationBuilder.RenameIndex(
            name: "IX_admin_accounts_Provider_Subject",
            table: "admin_accounts",
            newName: "ix_admin_accounts_provider_subject");

        migrationBuilder.RenameIndex(
            name: "IX_admin_accounts_UserId",
            table: "admin_accounts",
            newName: "ix_admin_accounts_user_id");

        migrationBuilder.RenameColumn(
            name: "Token",
            table: "refresh_tokens",
            newName: "token");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "refresh_tokens",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "refresh_tokens",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "OperatingSystem",
            table: "refresh_tokens",
            newName: "operating_system");

        migrationBuilder.RenameColumn(
            name: "ExpiresAt",
            table: "refresh_tokens",
            newName: "expires_at");

        migrationBuilder.RenameColumn(
            name: "DeviceName",
            table: "refresh_tokens",
            newName: "device_name");

        migrationBuilder.RenameColumn(
            name: "DeviceId",
            table: "refresh_tokens",
            newName: "device_id");

        migrationBuilder.RenameColumn(
            name: "CreatedAt",
            table: "refresh_tokens",
            newName: "created_at");

        migrationBuilder.RenameIndex(
            name: "IX_RefreshTokens_UserId",
            table: "refresh_tokens",
            newName: "ix_refresh_tokens_user_id");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "oauth_bind_identities",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "oauth_bind_identities",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "RefreshToken",
            table: "oauth_bind_identities",
            newName: "refresh_token");

        migrationBuilder.RenameColumn(
            name: "ProviderKind",
            table: "oauth_bind_identities",
            newName: "provider_kind");

        migrationBuilder.RenameColumn(
            name: "ProviderId",
            table: "oauth_bind_identities",
            newName: "provider_id");

        migrationBuilder.RenameColumn(
            name: "ExpiresAt",
            table: "oauth_bind_identities",
            newName: "expires_at");

        migrationBuilder.RenameColumn(
            name: "DisplayName",
            table: "oauth_bind_identities",
            newName: "display_name");

        migrationBuilder.RenameColumn(
            name: "CreatedAt",
            table: "oauth_bind_identities",
            newName: "created_at");

        migrationBuilder.RenameIndex(
            name: "IX_OAuthBindIdentities_UserId",
            table: "oauth_bind_identities",
            newName: "ix_oauth_bind_identities_user_id");

        migrationBuilder.RenameColumn(
            name: "Value",
            table: "asp_net_user_tokens",
            newName: "value");

        migrationBuilder.RenameColumn(
            name: "Name",
            table: "asp_net_user_tokens",
            newName: "name");

        migrationBuilder.RenameColumn(
            name: "LoginProvider",
            table: "asp_net_user_tokens",
            newName: "login_provider");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "asp_net_user_tokens",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "Email",
            table: "asp_net_users",
            newName: "email");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "asp_net_users",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UserName",
            table: "asp_net_users",
            newName: "user_name");

        migrationBuilder.RenameColumn(
            name: "TwoFactorEnabled",
            table: "asp_net_users",
            newName: "two_factor_enabled");

        migrationBuilder.RenameColumn(
            name: "SecurityStamp",
            table: "asp_net_users",
            newName: "security_stamp");

        migrationBuilder.RenameColumn(
            name: "PhoneNumberConfirmed",
            table: "asp_net_users",
            newName: "phone_number_confirmed");

        migrationBuilder.RenameColumn(
            name: "PhoneNumber",
            table: "asp_net_users",
            newName: "phone_number");

        migrationBuilder.RenameColumn(
            name: "PasswordHash",
            table: "asp_net_users",
            newName: "password_hash");

        migrationBuilder.RenameColumn(
            name: "NormalizedUserName",
            table: "asp_net_users",
            newName: "normalized_user_name");

        migrationBuilder.RenameColumn(
            name: "NormalizedEmail",
            table: "asp_net_users",
            newName: "normalized_email");

        migrationBuilder.RenameColumn(
            name: "LockoutEnd",
            table: "asp_net_users",
            newName: "lockout_end");

        migrationBuilder.RenameColumn(
            name: "LockoutEnabled",
            table: "asp_net_users",
            newName: "lockout_enabled");

        migrationBuilder.RenameColumn(
            name: "IsMaintainer",
            table: "asp_net_users",
            newName: "is_maintainer");

        migrationBuilder.RenameColumn(
            name: "IsLicensedDeveloper",
            table: "asp_net_users",
            newName: "is_licensed_developer");

        migrationBuilder.RenameColumn(
            name: "GachaLogExpireAt",
            table: "asp_net_users",
            newName: "gacha_log_expire_at");

        migrationBuilder.RenameColumn(
            name: "EmailConfirmed",
            table: "asp_net_users",
            newName: "email_confirmed");

        migrationBuilder.RenameColumn(
            name: "CreatedAt",
            table: "asp_net_users",
            newName: "created_at");

        migrationBuilder.RenameColumn(
            name: "ConcurrencyStamp",
            table: "asp_net_users",
            newName: "concurrency_stamp");

        migrationBuilder.RenameColumn(
            name: "CdnExpireAt",
            table: "asp_net_users",
            newName: "cdn_expire_at");

        migrationBuilder.RenameColumn(
            name: "AccessFailedCount",
            table: "asp_net_users",
            newName: "access_failed_count");

        migrationBuilder.RenameIndex(
            name: "UserNameIndex",
            table: "asp_net_users",
            newName: "user_name_index");

        migrationBuilder.RenameIndex(
            name: "EmailIndex",
            table: "asp_net_users",
            newName: "email_index");

        migrationBuilder.RenameColumn(
            name: "RoleId",
            table: "asp_net_user_roles",
            newName: "role_id");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "asp_net_user_roles",
            newName: "user_id");

        migrationBuilder.RenameIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "asp_net_user_roles",
            newName: "ix_asp_net_user_roles_role_id");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "asp_net_user_logins",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "ProviderDisplayName",
            table: "asp_net_user_logins",
            newName: "provider_display_name");

        migrationBuilder.RenameColumn(
            name: "ProviderKey",
            table: "asp_net_user_logins",
            newName: "provider_key");

        migrationBuilder.RenameColumn(
            name: "LoginProvider",
            table: "asp_net_user_logins",
            newName: "login_provider");

        migrationBuilder.RenameIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "asp_net_user_logins",
            newName: "ix_asp_net_user_logins_user_id");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "asp_net_user_claims",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "UserId",
            table: "asp_net_user_claims",
            newName: "user_id");

        migrationBuilder.RenameColumn(
            name: "ClaimValue",
            table: "asp_net_user_claims",
            newName: "claim_value");

        migrationBuilder.RenameColumn(
            name: "ClaimType",
            table: "asp_net_user_claims",
            newName: "claim_type");

        migrationBuilder.RenameIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "asp_net_user_claims",
            newName: "ix_asp_net_user_claims_user_id");

        migrationBuilder.RenameColumn(
            name: "Name",
            table: "asp_net_roles",
            newName: "name");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "asp_net_roles",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "NormalizedName",
            table: "asp_net_roles",
            newName: "normalized_name");

        migrationBuilder.RenameColumn(
            name: "ConcurrencyStamp",
            table: "asp_net_roles",
            newName: "concurrency_stamp");

        migrationBuilder.RenameIndex(
            name: "RoleNameIndex",
            table: "asp_net_roles",
            newName: "role_name_index");

        migrationBuilder.RenameColumn(
            name: "Id",
            table: "asp_net_role_claims",
            newName: "id");

        migrationBuilder.RenameColumn(
            name: "RoleId",
            table: "asp_net_role_claims",
            newName: "role_id");

        migrationBuilder.RenameColumn(
            name: "ClaimValue",
            table: "asp_net_role_claims",
            newName: "claim_value");

        migrationBuilder.RenameColumn(
            name: "ClaimType",
            table: "asp_net_role_claims",
            newName: "claim_type");

        migrationBuilder.RenameIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "asp_net_role_claims",
            newName: "ix_asp_net_role_claims_role_id");

        migrationBuilder.Sql("UPDATE `spiral_abysses_statistics` SET `name` = LEFT(`name`, 64) WHERE `name` IS NOT NULL AND CHAR_LENGTH(`name`) > 64;");

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "spiral_abysses_statistics",
            type: "varchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "user_name",
            table: "registration_records",
            type: "varchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `redeem_codes` SET `description` = LEFT(`description`, 1024) WHERE `description` IS NOT NULL AND CHAR_LENGTH(`description`) > 1024;");

        migrationBuilder.AlterColumn<string>(
            name: "description",
            table: "redeem_codes",
            type: "varchar(1024)",
            maxLength: 1024,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `redeem_codes` SET `creator` = LEFT(`creator`, 128) WHERE `creator` IS NOT NULL AND CHAR_LENGTH(`creator`) > 128;");

        migrationBuilder.AlterColumn<string>(
            name: "creator",
            table: "redeem_codes",
            type: "varchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `license_application_records` SET `project_url` = LEFT(`project_url`, 512) WHERE `project_url` IS NOT NULL AND CHAR_LENGTH(`project_url`) > 512;");

        migrationBuilder.AlterColumn<string>(
            name: "project_url",
            table: "license_application_records",
            type: "varchar(512)",
            maxLength: 512,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `license_application_records` SET `approval_code` = LEFT(`approval_code`, 64) WHERE `approval_code` IS NOT NULL AND CHAR_LENGTH(`approval_code`) > 64;");

        migrationBuilder.AlterColumn<string>(
            name: "approval_code",
            table: "license_application_records",
            type: "varchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `hutao_logs` SET `version` = LEFT(`version`, 32) WHERE `version` IS NOT NULL AND CHAR_LENGTH(`version`) > 32;");

        migrationBuilder.AlterColumn<string>(
            name: "version",
            table: "hutao_logs",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `gacha_statistics` SET `name` = LEFT(`name`, 64) WHERE `name` IS NOT NULL AND CHAR_LENGTH(`name`) > 64;");

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "gacha_statistics",
            type: "varchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `gacha_events` SET `locale` = LEFT(`locale`, 16) WHERE `locale` IS NOT NULL AND CHAR_LENGTH(`locale`) > 16;");

        migrationBuilder.AlterColumn<string>(
            name: "locale",
            table: "gacha_events",
            type: "varchar(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "gacha_events",
            type: "varchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `gacha_events` SET `version` = LEFT(`version`, 32) WHERE `version` IS NOT NULL AND CHAR_LENGTH(`version`) > 32;");

        migrationBuilder.AlterColumn<string>(
            name: "version",
            table: "gacha_events",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `banned` SET `reason` = LEFT(`reason`, 512) WHERE `reason` IS NOT NULL AND CHAR_LENGTH(`reason`) > 512;");

        migrationBuilder.AlterColumn<string>(
            name: "reason",
            table: "banned",
            type: "varchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `announcements` SET `title` = LEFT(`title`, 256) WHERE `title` IS NOT NULL AND CHAR_LENGTH(`title`) > 256;");

        migrationBuilder.AlterColumn<string>(
            name: "title",
            table: "announcements",
            type: "varchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `announcements` SET `locale` = LEFT(`locale`, 16) WHERE `locale` IS NOT NULL AND CHAR_LENGTH(`locale`) > 16;");

        migrationBuilder.AlterColumn<string>(
            name: "locale",
            table: "announcements",
            type: "varchar(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `announcements` SET `max_present_version` = LEFT(`max_present_version`, 32) WHERE `max_present_version` IS NOT NULL AND CHAR_LENGTH(`max_present_version`) > 32;");

        migrationBuilder.AlterColumn<string>(
            name: "max_present_version",
            table: "announcements",
            type: "varchar(32)",
            maxLength: 32,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `allowed_versions` SET `header` = LEFT(`header`, 64) WHERE `header` IS NOT NULL AND CHAR_LENGTH(`header`) > 64;");

        migrationBuilder.AlterColumn<string>(
            name: "header",
            table: "allowed_versions",
            type: "varchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `admin_audit_logs` SET `action` = LEFT(`action`, 128) WHERE `action` IS NOT NULL AND CHAR_LENGTH(`action`) > 128;");

        migrationBuilder.AlterColumn<string>(
            name: "action",
            table: "admin_audit_logs",
            type: "varchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `refresh_tokens` SET `operating_system` = LEFT(`operating_system`, 64) WHERE `operating_system` IS NOT NULL AND CHAR_LENGTH(`operating_system`) > 64;");

        migrationBuilder.AlterColumn<string>(
            name: "operating_system",
            table: "refresh_tokens",
            type: "varchar(64)",
            maxLength: 64,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `refresh_tokens` SET `device_name` = LEFT(`device_name`, 128) WHERE `device_name` IS NOT NULL AND CHAR_LENGTH(`device_name`) > 128;");

        migrationBuilder.AlterColumn<string>(
            name: "device_name",
            table: "refresh_tokens",
            type: "varchar(128)",
            maxLength: 128,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `refresh_tokens` SET `device_id` = LEFT(`device_id`, 64) WHERE `device_id` IS NOT NULL AND CHAR_LENGTH(`device_id`) > 64;");

        migrationBuilder.AlterColumn<string>(
            name: "device_id",
            table: "refresh_tokens",
            type: "varchar(64)",
            maxLength: 64,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `oauth_bind_identities` SET `refresh_token` = LEFT(`refresh_token`, 1024) WHERE `refresh_token` IS NOT NULL AND CHAR_LENGTH(`refresh_token`) > 1024;");

        migrationBuilder.AlterColumn<string>(
            name: "refresh_token",
            table: "oauth_bind_identities",
            type: "varchar(1024)",
            maxLength: 1024,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `oauth_bind_identities` SET `provider_id` = LEFT(`provider_id`, 256) WHERE `provider_id` IS NOT NULL AND CHAR_LENGTH(`provider_id`) > 256;");

        migrationBuilder.AlterColumn<string>(
            name: "provider_id",
            table: "oauth_bind_identities",
            type: "varchar(256)",
            maxLength: 256,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.Sql("UPDATE `oauth_bind_identities` SET `display_name` = LEFT(`display_name`, 128) WHERE `display_name` IS NOT NULL AND CHAR_LENGTH(`display_name`) > 128;");

        migrationBuilder.AlterColumn<string>(
            name: "display_name",
            table: "oauth_bind_identities",
            type: "varchar(128)",
            maxLength: 128,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext")
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddPrimaryKey(
            name: "PK_refresh_tokens",
            table: "refresh_tokens",
            column: "id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_oauth_bind_identities",
            table: "oauth_bind_identities",
            column: "id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_user_tokens",
            table: "asp_net_user_tokens",
            columns: new[] { "user_id", "login_provider", "name" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_users",
            table: "asp_net_users",
            column: "id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_user_roles",
            table: "asp_net_user_roles",
            columns: new[] { "user_id", "role_id" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_user_logins",
            table: "asp_net_user_logins",
            columns: new[] { "login_provider", "provider_key" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_user_claims",
            table: "asp_net_user_claims",
            column: "id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_roles",
            table: "asp_net_roles",
            column: "id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_asp_net_role_claims",
            table: "asp_net_role_claims",
            column: "id");

        migrationBuilder.AddForeignKey(
            name: "fk_admin_accounts_asp_net_users_user_id",
            table: "admin_accounts",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id");

        migrationBuilder.AddForeignKey(
            name: "fk_asp_net_role_claims_asp_net_roles_role_id",
            table: "asp_net_role_claims",
            column: "role_id",
            principalTable: "asp_net_roles",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_asp_net_user_claims_asp_net_users_user_id",
            table: "asp_net_user_claims",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_asp_net_user_logins_asp_net_users_user_id",
            table: "asp_net_user_logins",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_asp_net_user_roles_asp_net_roles_role_id",
            table: "asp_net_user_roles",
            column: "role_id",
            principalTable: "asp_net_roles",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_asp_net_user_roles_asp_net_users_user_id",
            table: "asp_net_user_roles",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_asp_net_user_tokens_asp_net_users_user_id",
            table: "asp_net_user_tokens",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_avatars_records_record_id",
            table: "avatars",
            column: "record_id",
            principalTable: "records",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_damage_ranks_spiral_abysses_spiral_abyss_id",
            table: "damage_ranks",
            column: "spiral_abyss_id",
            principalTable: "spiral_abysses",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_gacha_items_asp_net_users_user_id",
            table: "gacha_items",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_hutao_log_items_hutao_logs_log_id",
            table: "hutao_log_items",
            column: "log_id",
            principalTable: "hutao_logs",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_license_application_records_asp_net_users_user_id",
            table: "license_application_records",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_oauth_bind_identities_asp_net_users_user_id",
            table: "oauth_bind_identities",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_redeem_code_use_items_asp_net_users_used_by",
            table: "redeem_code_use_items",
            column: "used_by",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_redeem_code_use_items_redeem_codes_redeem_code_id",
            table: "redeem_code_use_items",
            column: "redeem_code_id",
            principalTable: "redeem_codes",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_refresh_tokens_asp_net_users_user_id",
            table: "refresh_tokens",
            column: "user_id",
            principalTable: "asp_net_users",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_role_combat_avatars_role_combat_records_record_id",
            table: "role_combat_avatars",
            column: "record_id",
            principalTable: "role_combat_records",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_spiral_abysses_records_record_id",
            table: "spiral_abysses",
            column: "record_id",
            principalTable: "records",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_spiral_abysses_floors_spiral_abysses_spiral_abyss_id",
            table: "spiral_abysses_floors",
            column: "spiral_abyss_id",
            principalTable: "spiral_abysses",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "fk_take_damage_ranks_spiral_abysses_spiral_abyss_id",
            table: "take_damage_ranks",
            column: "spiral_abyss_id",
            principalTable: "spiral_abysses",
            principalColumn: "primary_id",
            onDelete: ReferentialAction.Cascade);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_admin_accounts_asp_net_users_user_id",
            table: "admin_accounts");

        migrationBuilder.DropForeignKey(
            name: "fk_asp_net_role_claims_asp_net_roles_role_id",
            table: "asp_net_role_claims");

        migrationBuilder.DropForeignKey(
            name: "fk_asp_net_user_claims_asp_net_users_user_id",
            table: "asp_net_user_claims");

        migrationBuilder.DropForeignKey(
            name: "fk_asp_net_user_logins_asp_net_users_user_id",
            table: "asp_net_user_logins");

        migrationBuilder.DropForeignKey(
            name: "fk_asp_net_user_roles_asp_net_roles_role_id",
            table: "asp_net_user_roles");

        migrationBuilder.DropForeignKey(
            name: "fk_asp_net_user_roles_asp_net_users_user_id",
            table: "asp_net_user_roles");

        migrationBuilder.DropForeignKey(
            name: "fk_asp_net_user_tokens_asp_net_users_user_id",
            table: "asp_net_user_tokens");

        migrationBuilder.DropForeignKey(
            name: "fk_avatars_records_record_id",
            table: "avatars");

        migrationBuilder.DropForeignKey(
            name: "fk_damage_ranks_spiral_abysses_spiral_abyss_id",
            table: "damage_ranks");

        migrationBuilder.DropForeignKey(
            name: "fk_gacha_items_asp_net_users_user_id",
            table: "gacha_items");

        migrationBuilder.DropForeignKey(
            name: "fk_hutao_log_items_hutao_logs_log_id",
            table: "hutao_log_items");

        migrationBuilder.DropForeignKey(
            name: "fk_license_application_records_asp_net_users_user_id",
            table: "license_application_records");

        migrationBuilder.DropForeignKey(
            name: "fk_oauth_bind_identities_asp_net_users_user_id",
            table: "oauth_bind_identities");

        migrationBuilder.DropForeignKey(
            name: "fk_redeem_code_use_items_asp_net_users_used_by",
            table: "redeem_code_use_items");

        migrationBuilder.DropForeignKey(
            name: "fk_redeem_code_use_items_redeem_codes_redeem_code_id",
            table: "redeem_code_use_items");

        migrationBuilder.DropForeignKey(
            name: "fk_refresh_tokens_asp_net_users_user_id",
            table: "refresh_tokens");

        migrationBuilder.DropForeignKey(
            name: "fk_role_combat_avatars_role_combat_records_record_id",
            table: "role_combat_avatars");

        migrationBuilder.DropForeignKey(
            name: "fk_spiral_abysses_records_record_id",
            table: "spiral_abysses");

        migrationBuilder.DropForeignKey(
            name: "fk_spiral_abysses_floors_spiral_abysses_spiral_abyss_id",
            table: "spiral_abysses_floors");

        migrationBuilder.DropForeignKey(
            name: "fk_take_damage_ranks_spiral_abysses_spiral_abyss_id",
            table: "take_damage_ranks");

        migrationBuilder.DropPrimaryKey(
            name: "PK_refresh_tokens",
            table: "refresh_tokens");

        migrationBuilder.DropPrimaryKey(
            name: "PK_oauth_bind_identities",
            table: "oauth_bind_identities");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_users",
            table: "asp_net_users");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_user_tokens",
            table: "asp_net_user_tokens");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_user_roles",
            table: "asp_net_user_roles");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_user_logins",
            table: "asp_net_user_logins");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_user_claims",
            table: "asp_net_user_claims");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_roles",
            table: "asp_net_roles");

        migrationBuilder.DropPrimaryKey(
            name: "PK_asp_net_role_claims",
            table: "asp_net_role_claims");

        migrationBuilder.RenameTable(
            name: "refresh_tokens",
            newName: "RefreshTokens");

        migrationBuilder.RenameTable(
            name: "oauth_bind_identities",
            newName: "OAuthBindIdentities");

        migrationBuilder.RenameTable(
            name: "asp_net_users",
            newName: "AspNetUsers");

        migrationBuilder.RenameTable(
            name: "asp_net_user_tokens",
            newName: "AspNetUserTokens");

        migrationBuilder.RenameTable(
            name: "asp_net_user_roles",
            newName: "AspNetUserRoles");

        migrationBuilder.RenameTable(
            name: "asp_net_user_logins",
            newName: "AspNetUserLogins");

        migrationBuilder.RenameTable(
            name: "asp_net_user_claims",
            newName: "AspNetUserClaims");

        migrationBuilder.RenameTable(
            name: "asp_net_roles",
            newName: "AspNetRoles");

        migrationBuilder.RenameTable(
            name: "asp_net_role_claims",
            newName: "AspNetRoleClaims");

        migrationBuilder.RenameColumn(
            name: "payload",
            table: "yae_achievement_metadata",
            newName: "Payload");

        migrationBuilder.RenameColumn(
            name: "version",
            table: "yae_achievement_metadata",
            newName: "Version");

        migrationBuilder.RenameColumn(
            name: "synced_at",
            table: "yae_achievement_metadata",
            newName: "SyncedAt");

        migrationBuilder.RenameColumn(
            name: "store_cmd_id",
            table: "yae_achievement_metadata",
            newName: "StoreCmdId");

        migrationBuilder.RenameColumn(
            name: "oversea_hash",
            table: "yae_achievement_metadata",
            newName: "OverseaHash");

        migrationBuilder.RenameColumn(
            name: "item_count",
            table: "yae_achievement_metadata",
            newName: "ItemCount");

        migrationBuilder.RenameColumn(
            name: "group_count",
            table: "yae_achievement_metadata",
            newName: "GroupCount");

        migrationBuilder.RenameColumn(
            name: "chinese_hash",
            table: "yae_achievement_metadata",
            newName: "ChineseHash");

        migrationBuilder.RenameColumn(
            name: "achievement_cmd_id",
            table: "yae_achievement_metadata",
            newName: "AchievementCmdId");

        migrationBuilder.RenameColumn(
            name: "success",
            table: "user_login_logs",
            newName: "Success");

        migrationBuilder.RenameColumn(
            name: "method",
            table: "user_login_logs",
            newName: "Method");

        migrationBuilder.RenameColumn(
            name: "ip",
            table: "user_login_logs",
            newName: "Ip");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "user_login_logs",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "user_name",
            table: "user_login_logs",
            newName: "UserName");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "user_login_logs",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "operating_system",
            table: "user_login_logs",
            newName: "OperatingSystem");

        migrationBuilder.RenameColumn(
            name: "login_at",
            table: "user_login_logs",
            newName: "LoginAt");

        migrationBuilder.RenameColumn(
            name: "failure_key",
            table: "user_login_logs",
            newName: "FailureKey");

        migrationBuilder.RenameColumn(
            name: "device_name",
            table: "user_login_logs",
            newName: "DeviceName");

        migrationBuilder.RenameColumn(
            name: "device_id",
            table: "user_login_logs",
            newName: "DeviceId");

        migrationBuilder.RenameIndex(
            name: "ix_user_login_logs_ip",
            table: "user_login_logs",
            newName: "IX_user_login_logs_Ip");

        migrationBuilder.RenameIndex(
            name: "ix_user_login_logs_user_id",
            table: "user_login_logs",
            newName: "IX_user_login_logs_UserId");

        migrationBuilder.RenameIndex(
            name: "ix_user_login_logs_login_at",
            table: "user_login_logs",
            newName: "IX_user_login_logs_LoginAt");

        migrationBuilder.RenameColumn(
            name: "value",
            table: "take_damage_ranks",
            newName: "Value");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "take_damage_ranks",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "spiral_abyss_id",
            table: "take_damage_ranks",
            newName: "SpiralAbyssId");

        migrationBuilder.RenameColumn(
            name: "avatar_id",
            table: "take_damage_ranks",
            newName: "AvatarId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "take_damage_ranks",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_take_damage_ranks_value",
            table: "take_damage_ranks",
            newName: "IX_take_damage_ranks_Value");

        migrationBuilder.RenameIndex(
            name: "ix_take_damage_ranks_spiral_abyss_id",
            table: "take_damage_ranks",
            newName: "IX_take_damage_ranks_SpiralAbyssId");

        migrationBuilder.RenameColumn(
            name: "value",
            table: "system_settings",
            newName: "Value");

        migrationBuilder.RenameColumn(
            name: "key",
            table: "system_settings",
            newName: "Key");

        migrationBuilder.RenameColumn(
            name: "updated_by",
            table: "system_settings",
            newName: "UpdatedBy");

        migrationBuilder.RenameColumn(
            name: "updated_at",
            table: "system_settings",
            newName: "UpdatedAt");

        migrationBuilder.RenameColumn(
            name: "name",
            table: "spiral_abysses_statistics",
            newName: "Name");

        migrationBuilder.RenameColumn(
            name: "data",
            table: "spiral_abysses_statistics",
            newName: "Data");

        migrationBuilder.RenameColumn(
            name: "schedule_id",
            table: "spiral_abysses_statistics",
            newName: "ScheduleId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "spiral_abysses_statistics",
            newName: "PrimaryId");

        migrationBuilder.RenameColumn(
            name: "star",
            table: "spiral_abysses_floors",
            newName: "Star");

        migrationBuilder.RenameColumn(
            name: "levels",
            table: "spiral_abysses_floors",
            newName: "Levels");

        migrationBuilder.RenameColumn(
            name: "index",
            table: "spiral_abysses_floors",
            newName: "Index");

        migrationBuilder.RenameColumn(
            name: "spiral_abyss_id",
            table: "spiral_abysses_floors",
            newName: "SpiralAbyssId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "spiral_abysses_floors",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_spiral_abysses_floors_spiral_abyss_id",
            table: "spiral_abysses_floors",
            newName: "IX_spiral_abysses_floors_SpiralAbyssId");

        migrationBuilder.RenameColumn(
            name: "total_win_times",
            table: "spiral_abysses",
            newName: "TotalWinTimes");

        migrationBuilder.RenameColumn(
            name: "total_battle_times",
            table: "spiral_abysses",
            newName: "TotalBattleTimes");

        migrationBuilder.RenameColumn(
            name: "record_id",
            table: "spiral_abysses",
            newName: "RecordId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "spiral_abysses",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_spiral_abysses_record_id",
            table: "spiral_abysses",
            newName: "IX_spiral_abysses_RecordId");

        migrationBuilder.RenameColumn(
            name: "data",
            table: "role_combat_statistics",
            newName: "Data");

        migrationBuilder.RenameColumn(
            name: "schedule_id",
            table: "role_combat_statistics",
            newName: "ScheduleId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "role_combat_statistics",
            newName: "PrimaryId");

        migrationBuilder.RenameColumn(
            name: "uploader",
            table: "role_combat_records",
            newName: "Uploader");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "role_combat_records",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "upload_time",
            table: "role_combat_records",
            newName: "UploadTime");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "role_combat_records",
            newName: "PrimaryId");

        migrationBuilder.RenameColumn(
            name: "record_id",
            table: "role_combat_avatars",
            newName: "RecordId");

        migrationBuilder.RenameColumn(
            name: "avatar_id",
            table: "role_combat_avatars",
            newName: "AvatarId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "role_combat_avatars",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_role_combat_avatars_record_id",
            table: "role_combat_avatars",
            newName: "IX_role_combat_avatars_RecordId");

        migrationBuilder.RenameColumn(
            name: "user_name",
            table: "registration_records",
            newName: "UserName");

        migrationBuilder.RenameColumn(
            name: "value",
            table: "redeem_codes",
            newName: "Value");

        migrationBuilder.RenameColumn(
            name: "type",
            table: "redeem_codes",
            newName: "Type");

        migrationBuilder.RenameColumn(
            name: "description",
            table: "redeem_codes",
            newName: "Description");

        migrationBuilder.RenameColumn(
            name: "creator",
            table: "redeem_codes",
            newName: "Creator");

        migrationBuilder.RenameColumn(
            name: "code",
            table: "redeem_codes",
            newName: "Code");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "redeem_codes",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "times_allowed",
            table: "redeem_codes",
            newName: "TimesAllowed");

        migrationBuilder.RenameColumn(
            name: "service_type",
            table: "redeem_codes",
            newName: "ServiceType");

        migrationBuilder.RenameColumn(
            name: "expire_time",
            table: "redeem_codes",
            newName: "ExpireTime");

        migrationBuilder.RenameColumn(
            name: "create_time",
            table: "redeem_codes",
            newName: "CreateTime");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "redeem_code_use_items",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "used_by",
            table: "redeem_code_use_items",
            newName: "UsedBy");

        migrationBuilder.RenameColumn(
            name: "use_time",
            table: "redeem_code_use_items",
            newName: "UseTime");

        migrationBuilder.RenameColumn(
            name: "redeem_code_id",
            table: "redeem_code_use_items",
            newName: "RedeemCodeId");

        migrationBuilder.RenameIndex(
            name: "ix_redeem_code_use_items_used_by",
            table: "redeem_code_use_items",
            newName: "IX_redeem_code_use_items_UsedBy");

        migrationBuilder.RenameIndex(
            name: "ix_redeem_code_use_items_redeem_code_id",
            table: "redeem_code_use_items",
            newName: "IX_redeem_code_use_items_RedeemCodeId");

        migrationBuilder.RenameColumn(
            name: "uploader",
            table: "records",
            newName: "Uploader");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "records",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "upload_time",
            table: "records",
            newName: "UploadTime");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "records",
            newName: "PrimaryId");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "license_application_records",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "project_url",
            table: "license_application_records",
            newName: "ProjectUrl");

        migrationBuilder.RenameColumn(
            name: "is_approved",
            table: "license_application_records",
            newName: "IsApproved");

        migrationBuilder.RenameColumn(
            name: "approval_code",
            table: "license_application_records",
            newName: "ApprovalCode");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "license_application_records",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_license_application_records_user_id",
            table: "license_application_records",
            newName: "IX_license_application_records_UserId");

        migrationBuilder.RenameColumn(
            name: "quality",
            table: "known_items",
            newName: "Quality");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "known_items",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "invalid_gacha_uids",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "version",
            table: "hutao_releases",
            newName: "Version");

        migrationBuilder.RenameColumn(
            name: "validation",
            table: "hutao_releases",
            newName: "Validation");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "hutao_releases",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "release_notes",
            table: "hutao_releases",
            newName: "ReleaseNotes");

        migrationBuilder.RenameColumn(
            name: "published_by",
            table: "hutao_releases",
            newName: "PublishedBy");

        migrationBuilder.RenameColumn(
            name: "published_at",
            table: "hutao_releases",
            newName: "PublishedAt");

        migrationBuilder.RenameColumn(
            name: "mirrors_json",
            table: "hutao_releases",
            newName: "MirrorsJson");

        migrationBuilder.RenameColumn(
            name: "minimum_version",
            table: "hutao_releases",
            newName: "MinimumVersion");

        migrationBuilder.RenameColumn(
            name: "is_active",
            table: "hutao_releases",
            newName: "IsActive");

        migrationBuilder.RenameIndex(
            name: "ix_hutao_releases_version",
            table: "hutao_releases",
            newName: "IX_hutao_releases_Version");

        migrationBuilder.RenameIndex(
            name: "ix_hutao_releases_is_active",
            table: "hutao_releases",
            newName: "IX_hutao_releases_IsActive");

        migrationBuilder.RenameColumn(
            name: "version",
            table: "hutao_logs",
            newName: "Version");

        migrationBuilder.RenameColumn(
            name: "resolved",
            table: "hutao_logs",
            newName: "Resolved");

        migrationBuilder.RenameColumn(
            name: "info",
            table: "hutao_logs",
            newName: "Info");

        migrationBuilder.RenameColumn(
            name: "count",
            table: "hutao_logs",
            newName: "Count");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "hutao_logs",
            newName: "PrimaryId");

        migrationBuilder.RenameColumn(
            name: "time",
            table: "hutao_log_items",
            newName: "Time");

        migrationBuilder.RenameColumn(
            name: "log_id",
            table: "hutao_log_items",
            newName: "LogId");

        migrationBuilder.RenameColumn(
            name: "device_id",
            table: "hutao_log_items",
            newName: "DeviceId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "hutao_log_items",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_hutao_log_items_log_id",
            table: "hutao_log_items",
            newName: "IX_hutao_log_items_LogId");

        migrationBuilder.RenameColumn(
            name: "name",
            table: "gacha_statistics",
            newName: "Name");

        migrationBuilder.RenameColumn(
            name: "data",
            table: "gacha_statistics",
            newName: "Data");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "gacha_statistics",
            newName: "PrimaryId");

        migrationBuilder.RenameColumn(
            name: "time",
            table: "gacha_items",
            newName: "Time");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "gacha_items",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "gacha_items",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "gacha_items",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "query_type",
            table: "gacha_items",
            newName: "QueryType");

        migrationBuilder.RenameColumn(
            name: "item_id",
            table: "gacha_items",
            newName: "ItemId");

        migrationBuilder.RenameColumn(
            name: "is_trusted",
            table: "gacha_items",
            newName: "IsTrusted");

        migrationBuilder.RenameColumn(
            name: "gacha_type",
            table: "gacha_items",
            newName: "GachaType");

        migrationBuilder.RenameIndex(
            name: "ix_gacha_items_user_id",
            table: "gacha_items",
            newName: "IX_gacha_items_UserId");

        migrationBuilder.RenameColumn(
            name: "type",
            table: "gacha_events",
            newName: "Type");

        migrationBuilder.RenameColumn(
            name: "to",
            table: "gacha_events",
            newName: "To");

        migrationBuilder.RenameColumn(
            name: "from",
            table: "gacha_events",
            newName: "From");

        migrationBuilder.RenameColumn(
            name: "order",
            table: "gacha_events",
            newName: "Order");

        migrationBuilder.RenameColumn(
            name: "locale",
            table: "gacha_events",
            newName: "Locale");

        migrationBuilder.RenameColumn(
            name: "name",
            table: "gacha_events",
            newName: "Name");

        migrationBuilder.RenameColumn(
            name: "version",
            table: "gacha_events",
            newName: "Version");

        migrationBuilder.RenameColumn(
            name: "up_purple_item5",
            table: "gacha_events",
            newName: "UpPurpleItem5");

        migrationBuilder.RenameColumn(
            name: "up_purple_item4",
            table: "gacha_events",
            newName: "UpPurpleItem4");

        migrationBuilder.RenameColumn(
            name: "up_purple_item3",
            table: "gacha_events",
            newName: "UpPurpleItem3");

        migrationBuilder.RenameColumn(
            name: "up_purple_item2",
            table: "gacha_events",
            newName: "UpPurpleItem2");

        migrationBuilder.RenameColumn(
            name: "up_purple_item1",
            table: "gacha_events",
            newName: "UpPurpleItem1");

        migrationBuilder.RenameColumn(
            name: "up_orange_item9",
            table: "gacha_events",
            newName: "UpOrangeItem9");

        migrationBuilder.RenameColumn(
            name: "up_orange_item8",
            table: "gacha_events",
            newName: "UpOrangeItem8");

        migrationBuilder.RenameColumn(
            name: "up_orange_item7",
            table: "gacha_events",
            newName: "UpOrangeItem7");

        migrationBuilder.RenameColumn(
            name: "up_orange_item6",
            table: "gacha_events",
            newName: "UpOrangeItem6");

        migrationBuilder.RenameColumn(
            name: "up_orange_item5",
            table: "gacha_events",
            newName: "UpOrangeItem5");

        migrationBuilder.RenameColumn(
            name: "up_orange_item4",
            table: "gacha_events",
            newName: "UpOrangeItem4");

        migrationBuilder.RenameColumn(
            name: "up_orange_item3",
            table: "gacha_events",
            newName: "UpOrangeItem3");

        migrationBuilder.RenameColumn(
            name: "up_orange_item2",
            table: "gacha_events",
            newName: "UpOrangeItem2");

        migrationBuilder.RenameColumn(
            name: "up_orange_item17",
            table: "gacha_events",
            newName: "UpOrangeItem17");

        migrationBuilder.RenameColumn(
            name: "up_orange_item16",
            table: "gacha_events",
            newName: "UpOrangeItem16");

        migrationBuilder.RenameColumn(
            name: "up_orange_item15",
            table: "gacha_events",
            newName: "UpOrangeItem15");

        migrationBuilder.RenameColumn(
            name: "up_orange_item14",
            table: "gacha_events",
            newName: "UpOrangeItem14");

        migrationBuilder.RenameColumn(
            name: "up_orange_item13",
            table: "gacha_events",
            newName: "UpOrangeItem13");

        migrationBuilder.RenameColumn(
            name: "up_orange_item12",
            table: "gacha_events",
            newName: "UpOrangeItem12");

        migrationBuilder.RenameColumn(
            name: "up_orange_item11",
            table: "gacha_events",
            newName: "UpOrangeItem11");

        migrationBuilder.RenameColumn(
            name: "up_orange_item10",
            table: "gacha_events",
            newName: "UpOrangeItem10");

        migrationBuilder.RenameColumn(
            name: "up_orange_item1",
            table: "gacha_events",
            newName: "UpOrangeItem1");

        migrationBuilder.RenameColumn(
            name: "value",
            table: "damage_ranks",
            newName: "Value");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "damage_ranks",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "spiral_abyss_id",
            table: "damage_ranks",
            newName: "SpiralAbyssId");

        migrationBuilder.RenameColumn(
            name: "avatar_id",
            table: "damage_ranks",
            newName: "AvatarId");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "damage_ranks",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_damage_ranks_value",
            table: "damage_ranks",
            newName: "IX_damage_ranks_Value");

        migrationBuilder.RenameIndex(
            name: "ix_damage_ranks_spiral_abyss_id",
            table: "damage_ranks",
            newName: "IX_damage_ranks_SpiralAbyssId");

        migrationBuilder.RenameColumn(
            name: "version",
            table: "client_versions",
            newName: "Version");

        migrationBuilder.RenameColumn(
            name: "last_seen_at",
            table: "client_versions",
            newName: "LastSeenAt");

        migrationBuilder.RenameColumn(
            name: "first_seen_at",
            table: "client_versions",
            newName: "FirstSeenAt");

        migrationBuilder.RenameColumn(
            name: "device_id",
            table: "client_versions",
            newName: "DeviceId");

        migrationBuilder.RenameColumn(
            name: "reason",
            table: "banned",
            newName: "Reason");

        migrationBuilder.RenameColumn(
            name: "uid",
            table: "banned",
            newName: "Uid");

        migrationBuilder.RenameColumn(
            name: "ban_time",
            table: "banned",
            newName: "BanTime");

        migrationBuilder.RenameColumn(
            name: "weapon_id",
            table: "avatars",
            newName: "WeaponId");

        migrationBuilder.RenameColumn(
            name: "reliquary_set",
            table: "avatars",
            newName: "ReliquarySet");

        migrationBuilder.RenameColumn(
            name: "record_id",
            table: "avatars",
            newName: "RecordId");

        migrationBuilder.RenameColumn(
            name: "avatar_id",
            table: "avatars",
            newName: "AvatarId");

        migrationBuilder.RenameColumn(
            name: "actived_constellation_number",
            table: "avatars",
            newName: "ActivedConstellationNumber");

        migrationBuilder.RenameColumn(
            name: "primary_id",
            table: "avatars",
            newName: "PrimaryId");

        migrationBuilder.RenameIndex(
            name: "ix_avatars_record_id",
            table: "avatars",
            newName: "IX_avatars_RecordId");

        migrationBuilder.RenameColumn(
            name: "title",
            table: "announcements",
            newName: "Title");

        migrationBuilder.RenameColumn(
            name: "severity",
            table: "announcements",
            newName: "Severity");

        migrationBuilder.RenameColumn(
            name: "link",
            table: "announcements",
            newName: "Link");

        migrationBuilder.RenameColumn(
            name: "kind",
            table: "announcements",
            newName: "Kind");

        migrationBuilder.RenameColumn(
            name: "enabled",
            table: "announcements",
            newName: "Enabled");

        migrationBuilder.RenameColumn(
            name: "content",
            table: "announcements",
            newName: "Content");

        migrationBuilder.RenameColumn(
            name: "locale",
            table: "announcements",
            newName: "Locale");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "announcements",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "max_present_version",
            table: "announcements",
            newName: "MaxPresentVersion");

        migrationBuilder.RenameColumn(
            name: "last_update_time",
            table: "announcements",
            newName: "LastUpdateTime");

        migrationBuilder.RenameColumn(
            name: "header",
            table: "allowed_versions",
            newName: "Header");

        migrationBuilder.RenameColumn(
            name: "timestamp",
            table: "admin_audit_logs",
            newName: "Timestamp");

        migrationBuilder.RenameColumn(
            name: "success",
            table: "admin_audit_logs",
            newName: "Success");

        migrationBuilder.RenameColumn(
            name: "ip",
            table: "admin_audit_logs",
            newName: "Ip");

        migrationBuilder.RenameColumn(
            name: "detail",
            table: "admin_audit_logs",
            newName: "Detail");

        migrationBuilder.RenameColumn(
            name: "action",
            table: "admin_audit_logs",
            newName: "Action");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "admin_audit_logs",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "admin_user_id",
            table: "admin_audit_logs",
            newName: "AdminUserId");

        migrationBuilder.RenameColumn(
            name: "admin_account_name",
            table: "admin_audit_logs",
            newName: "AdminAccountName");

        migrationBuilder.RenameColumn(
            name: "admin_account_id",
            table: "admin_audit_logs",
            newName: "AdminAccountId");

        migrationBuilder.RenameColumn(
            name: "subject",
            table: "admin_accounts",
            newName: "Subject");

        migrationBuilder.RenameColumn(
            name: "provider",
            table: "admin_accounts",
            newName: "Provider");

        migrationBuilder.RenameColumn(
            name: "email",
            table: "admin_accounts",
            newName: "Email");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "admin_accounts",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "user_name",
            table: "admin_accounts",
            newName: "UserName");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "admin_accounts",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "last_login_at",
            table: "admin_accounts",
            newName: "LastLoginAt");

        migrationBuilder.RenameColumn(
            name: "is_enabled",
            table: "admin_accounts",
            newName: "IsEnabled");

        migrationBuilder.RenameColumn(
            name: "is_bootstrap",
            table: "admin_accounts",
            newName: "IsBootstrap");

        migrationBuilder.RenameColumn(
            name: "created_at",
            table: "admin_accounts",
            newName: "CreatedAt");

        migrationBuilder.RenameIndex(
            name: "ix_admin_accounts_provider_subject",
            table: "admin_accounts",
            newName: "IX_admin_accounts_Provider_Subject");

        migrationBuilder.RenameIndex(
            name: "ix_admin_accounts_user_id",
            table: "admin_accounts",
            newName: "IX_admin_accounts_UserId");

        migrationBuilder.RenameColumn(
            name: "token",
            table: "RefreshTokens",
            newName: "Token");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "RefreshTokens",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "RefreshTokens",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "operating_system",
            table: "RefreshTokens",
            newName: "OperatingSystem");

        migrationBuilder.RenameColumn(
            name: "expires_at",
            table: "RefreshTokens",
            newName: "ExpiresAt");

        migrationBuilder.RenameColumn(
            name: "device_name",
            table: "RefreshTokens",
            newName: "DeviceName");

        migrationBuilder.RenameColumn(
            name: "device_id",
            table: "RefreshTokens",
            newName: "DeviceId");

        migrationBuilder.RenameColumn(
            name: "created_at",
            table: "RefreshTokens",
            newName: "CreatedAt");

        migrationBuilder.RenameIndex(
            name: "ix_refresh_tokens_user_id",
            table: "RefreshTokens",
            newName: "IX_RefreshTokens_UserId");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "OAuthBindIdentities",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "OAuthBindIdentities",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "refresh_token",
            table: "OAuthBindIdentities",
            newName: "RefreshToken");

        migrationBuilder.RenameColumn(
            name: "provider_kind",
            table: "OAuthBindIdentities",
            newName: "ProviderKind");

        migrationBuilder.RenameColumn(
            name: "provider_id",
            table: "OAuthBindIdentities",
            newName: "ProviderId");

        migrationBuilder.RenameColumn(
            name: "expires_at",
            table: "OAuthBindIdentities",
            newName: "ExpiresAt");

        migrationBuilder.RenameColumn(
            name: "display_name",
            table: "OAuthBindIdentities",
            newName: "DisplayName");

        migrationBuilder.RenameColumn(
            name: "created_at",
            table: "OAuthBindIdentities",
            newName: "CreatedAt");

        migrationBuilder.RenameIndex(
            name: "ix_oauth_bind_identities_user_id",
            table: "OAuthBindIdentities",
            newName: "IX_OAuthBindIdentities_UserId");

        migrationBuilder.RenameColumn(
            name: "email",
            table: "AspNetUsers",
            newName: "Email");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "AspNetUsers",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "user_name",
            table: "AspNetUsers",
            newName: "UserName");

        migrationBuilder.RenameColumn(
            name: "two_factor_enabled",
            table: "AspNetUsers",
            newName: "TwoFactorEnabled");

        migrationBuilder.RenameColumn(
            name: "security_stamp",
            table: "AspNetUsers",
            newName: "SecurityStamp");

        migrationBuilder.RenameColumn(
            name: "phone_number_confirmed",
            table: "AspNetUsers",
            newName: "PhoneNumberConfirmed");

        migrationBuilder.RenameColumn(
            name: "phone_number",
            table: "AspNetUsers",
            newName: "PhoneNumber");

        migrationBuilder.RenameColumn(
            name: "password_hash",
            table: "AspNetUsers",
            newName: "PasswordHash");

        migrationBuilder.RenameColumn(
            name: "normalized_user_name",
            table: "AspNetUsers",
            newName: "NormalizedUserName");

        migrationBuilder.RenameColumn(
            name: "normalized_email",
            table: "AspNetUsers",
            newName: "NormalizedEmail");

        migrationBuilder.RenameColumn(
            name: "lockout_end",
            table: "AspNetUsers",
            newName: "LockoutEnd");

        migrationBuilder.RenameColumn(
            name: "lockout_enabled",
            table: "AspNetUsers",
            newName: "LockoutEnabled");

        migrationBuilder.RenameColumn(
            name: "is_maintainer",
            table: "AspNetUsers",
            newName: "IsMaintainer");

        migrationBuilder.RenameColumn(
            name: "is_licensed_developer",
            table: "AspNetUsers",
            newName: "IsLicensedDeveloper");

        migrationBuilder.RenameColumn(
            name: "gacha_log_expire_at",
            table: "AspNetUsers",
            newName: "GachaLogExpireAt");

        migrationBuilder.RenameColumn(
            name: "email_confirmed",
            table: "AspNetUsers",
            newName: "EmailConfirmed");

        migrationBuilder.RenameColumn(
            name: "created_at",
            table: "AspNetUsers",
            newName: "CreatedAt");

        migrationBuilder.RenameColumn(
            name: "concurrency_stamp",
            table: "AspNetUsers",
            newName: "ConcurrencyStamp");

        migrationBuilder.RenameColumn(
            name: "cdn_expire_at",
            table: "AspNetUsers",
            newName: "CdnExpireAt");

        migrationBuilder.RenameColumn(
            name: "access_failed_count",
            table: "AspNetUsers",
            newName: "AccessFailedCount");

        migrationBuilder.RenameIndex(
            name: "user_name_index",
            table: "AspNetUsers",
            newName: "UserNameIndex");

        migrationBuilder.RenameIndex(
            name: "email_index",
            table: "AspNetUsers",
            newName: "EmailIndex");

        migrationBuilder.RenameColumn(
            name: "value",
            table: "AspNetUserTokens",
            newName: "Value");

        migrationBuilder.RenameColumn(
            name: "name",
            table: "AspNetUserTokens",
            newName: "Name");

        migrationBuilder.RenameColumn(
            name: "login_provider",
            table: "AspNetUserTokens",
            newName: "LoginProvider");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "AspNetUserTokens",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "role_id",
            table: "AspNetUserRoles",
            newName: "RoleId");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "AspNetUserRoles",
            newName: "UserId");

        migrationBuilder.RenameIndex(
            name: "ix_asp_net_user_roles_role_id",
            table: "AspNetUserRoles",
            newName: "IX_AspNetUserRoles_RoleId");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "AspNetUserLogins",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "provider_display_name",
            table: "AspNetUserLogins",
            newName: "ProviderDisplayName");

        migrationBuilder.RenameColumn(
            name: "provider_key",
            table: "AspNetUserLogins",
            newName: "ProviderKey");

        migrationBuilder.RenameColumn(
            name: "login_provider",
            table: "AspNetUserLogins",
            newName: "LoginProvider");

        migrationBuilder.RenameIndex(
            name: "ix_asp_net_user_logins_user_id",
            table: "AspNetUserLogins",
            newName: "IX_AspNetUserLogins_UserId");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "AspNetUserClaims",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "user_id",
            table: "AspNetUserClaims",
            newName: "UserId");

        migrationBuilder.RenameColumn(
            name: "claim_value",
            table: "AspNetUserClaims",
            newName: "ClaimValue");

        migrationBuilder.RenameColumn(
            name: "claim_type",
            table: "AspNetUserClaims",
            newName: "ClaimType");

        migrationBuilder.RenameIndex(
            name: "ix_asp_net_user_claims_user_id",
            table: "AspNetUserClaims",
            newName: "IX_AspNetUserClaims_UserId");

        migrationBuilder.RenameColumn(
            name: "name",
            table: "AspNetRoles",
            newName: "Name");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "AspNetRoles",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "normalized_name",
            table: "AspNetRoles",
            newName: "NormalizedName");

        migrationBuilder.RenameColumn(
            name: "concurrency_stamp",
            table: "AspNetRoles",
            newName: "ConcurrencyStamp");

        migrationBuilder.RenameIndex(
            name: "role_name_index",
            table: "AspNetRoles",
            newName: "RoleNameIndex");

        migrationBuilder.RenameColumn(
            name: "id",
            table: "AspNetRoleClaims",
            newName: "Id");

        migrationBuilder.RenameColumn(
            name: "role_id",
            table: "AspNetRoleClaims",
            newName: "RoleId");

        migrationBuilder.RenameColumn(
            name: "claim_value",
            table: "AspNetRoleClaims",
            newName: "ClaimValue");

        migrationBuilder.RenameColumn(
            name: "claim_type",
            table: "AspNetRoleClaims",
            newName: "ClaimType");

        migrationBuilder.RenameIndex(
            name: "ix_asp_net_role_claims_role_id",
            table: "AspNetRoleClaims",
            newName: "IX_AspNetRoleClaims_RoleId");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "spiral_abysses_statistics",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "UserName",
            table: "registration_records",
            type: "varchar(255)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "redeem_codes",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(1024)",
            oldMaxLength: 1024)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Creator",
            table: "redeem_codes",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(128)",
            oldMaxLength: 128)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "ProjectUrl",
            table: "license_application_records",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(512)",
            oldMaxLength: 512)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "ApprovalCode",
            table: "license_application_records",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Version",
            table: "hutao_logs",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "gacha_statistics",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Locale",
            table: "gacha_events",
            type: "varchar(255)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(16)",
            oldMaxLength: 16)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Name",
            table: "gacha_events",
            type: "varchar(255)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Version",
            table: "gacha_events",
            type: "varchar(255)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Reason",
            table: "banned",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(512)",
            oldMaxLength: 512,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Title",
            table: "announcements",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Locale",
            table: "announcements",
            type: "varchar(255)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(16)",
            oldMaxLength: 16)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "MaxPresentVersion",
            table: "announcements",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Header",
            table: "allowed_versions",
            type: "varchar(255)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "Action",
            table: "admin_audit_logs",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(128)",
            oldMaxLength: 128)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "OperatingSystem",
            table: "RefreshTokens",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "DeviceName",
            table: "RefreshTokens",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(128)",
            oldMaxLength: 128,
            oldNullable: true)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "DeviceId",
            table: "RefreshTokens",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "RefreshToken",
            table: "OAuthBindIdentities",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(1024)",
            oldMaxLength: 1024)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "ProviderId",
            table: "OAuthBindIdentities",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(256)",
            oldMaxLength: 256)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AlterColumn<string>(
            name: "DisplayName",
            table: "OAuthBindIdentities",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(128)",
            oldMaxLength: 128)
            .Annotation("MySql:CharSet", "utf8mb4")
            .OldAnnotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddPrimaryKey(
            name: "PK_RefreshTokens",
            table: "RefreshTokens",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_OAuthBindIdentities",
            table: "OAuthBindIdentities",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetUsers",
            table: "AspNetUsers",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetUserTokens",
            table: "AspNetUserTokens",
            columns: new[] { "UserId", "LoginProvider", "Name" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetUserRoles",
            table: "AspNetUserRoles",
            columns: new[] { "UserId", "RoleId" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetUserLogins",
            table: "AspNetUserLogins",
            columns: new[] { "LoginProvider", "ProviderKey" });

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetUserClaims",
            table: "AspNetUserClaims",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetRoles",
            table: "AspNetRoles",
            column: "Id");

        migrationBuilder.AddPrimaryKey(
            name: "PK_AspNetRoleClaims",
            table: "AspNetRoleClaims",
            column: "Id");

        migrationBuilder.CreateTable(
            name: "achievement_archives",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                UserId = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_achievement_archives", x => x.Id);
                table.ForeignKey(
                    name: "FK_achievement_archives_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "passport_verifications",
            columns: table => new
            {
                NormalizedUserName = table.Column<string>(type: "varchar(255)", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ExpireTimestamp = table.Column<long>(type: "bigint", nullable: false),
                GeneratedTimestamp = table.Column<long>(type: "bigint", nullable: false),
                VerifyCode = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_passport_verifications", x => x.NormalizedUserName);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "request_statistics",
            columns: table => new
            {
                PrimaryId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Count = table.Column<long>(type: "bigint", nullable: false),
                Path = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserAgent = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_request_statistics", x => x.PrimaryId);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "achievements",
            columns: table => new
            {
                Id = table.Column<uint>(type: "int unsigned", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                ArchiveId = table.Column<long>(type: "bigint", nullable: false),
                Current = table.Column<uint>(type: "int unsigned", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Time = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_achievements", x => x.Id);
                table.ForeignKey(
                    name: "FK_achievements_achievement_archives_ArchiveId",
                    column: x => x.ArchiveId,
                    principalTable: "achievement_archives",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_achievement_archives_UserId",
            table: "achievement_archives",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_achievements_ArchiveId",
            table: "achievements",
            column: "ArchiveId");

        migrationBuilder.AddForeignKey(
            name: "FK_admin_accounts_AspNetUsers_UserId",
            table: "admin_accounts",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id");

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId",
            principalTable: "AspNetRoles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUserClaims_AspNetUsers_UserId",
            table: "AspNetUserClaims",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUserLogins_AspNetUsers_UserId",
            table: "AspNetUserLogins",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId",
            principalTable: "AspNetRoles",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUserRoles_AspNetUsers_UserId",
            table: "AspNetUserRoles",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_AspNetUserTokens_AspNetUsers_UserId",
            table: "AspNetUserTokens",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_avatars_records_RecordId",
            table: "avatars",
            column: "RecordId",
            principalTable: "records",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_damage_ranks_spiral_abysses_SpiralAbyssId",
            table: "damage_ranks",
            column: "SpiralAbyssId",
            principalTable: "spiral_abysses",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_gacha_items_AspNetUsers_UserId",
            table: "gacha_items",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_hutao_log_items_hutao_logs_LogId",
            table: "hutao_log_items",
            column: "LogId",
            principalTable: "hutao_logs",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_license_application_records_AspNetUsers_UserId",
            table: "license_application_records",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_OAuthBindIdentities_AspNetUsers_UserId",
            table: "OAuthBindIdentities",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_redeem_code_use_items_AspNetUsers_UsedBy",
            table: "redeem_code_use_items",
            column: "UsedBy",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_redeem_code_use_items_redeem_codes_RedeemCodeId",
            table: "redeem_code_use_items",
            column: "RedeemCodeId",
            principalTable: "redeem_codes",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_RefreshTokens_AspNetUsers_UserId",
            table: "RefreshTokens",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_role_combat_avatars_role_combat_records_RecordId",
            table: "role_combat_avatars",
            column: "RecordId",
            principalTable: "role_combat_records",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_spiral_abysses_records_RecordId",
            table: "spiral_abysses",
            column: "RecordId",
            principalTable: "records",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_spiral_abysses_floors_spiral_abysses_SpiralAbyssId",
            table: "spiral_abysses_floors",
            column: "SpiralAbyssId",
            principalTable: "spiral_abysses",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);

        migrationBuilder.AddForeignKey(
            name: "FK_take_damage_ranks_spiral_abysses_SpiralAbyssId",
            table: "take_damage_ranks",
            column: "SpiralAbyssId",
            principalTable: "spiral_abysses",
            principalColumn: "PrimaryId",
            onDelete: ReferentialAction.Cascade);
    }
}
