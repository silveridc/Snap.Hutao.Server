using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using server.Models.Context.Configuration;
using server.Models.Entity;
using server.Models.Entity.Achievement;
using server.Models.Entity.Announcement;
using server.Models.Entity.GachaLog;
using server.Models.Entity.Passport;
using server.Models.Entity.Redeem;
using server.Models.Entity.RoleCombat;
using server.Models.Entity.SpiralAbyss;
using server.Models.Entity.Telemetry;

namespace server.Models.Context
{
    public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<HutaoUser, IdentityRole<int>, int>(options)
    {
        #region Infrastructure
        public DbSet<RequestStatistics> RequestStatistics { get; set; } = default!;

        public DbSet<AllowedVersion> AllowedVersions { get; set; } = default!;

        public DbSet<LicenseApplicationRecord> Licenses { get; set; } = default!;

        public DbSet<EntityAnnouncement> Announcements { get; set; } = default!;

        public DbSet<PassportVerification> PassportVerifications { get; set; } = default!;

        public DbSet<RegistrationRecord> RegistrationRecords { get; set; } = default!;

        public DbSet<RedeemCode> RedeemCodes { get; set; } = default!;

        public DbSet<RedeemCodeUseItem> RedeemCodeUseItems { get; set; } = default!;

        public DbSet<AdminAuditLog> AdminAuditLogs { get; set; } = default!;

        public DbSet<HutaoRelease> HutaoReleases { get; set; } = default!;

        public DbSet<SystemSetting> SystemSettings { get; set; } = default!;
        #endregion

        #region Telemetry
        public DbSet<HutaoLog> HutaoLogs { get; set; } = default!;

        public DbSet<HutaoLogSingleItem> HutaoLogSingleItems { get; set; } = default!;
        #endregion

        #region SpiralAbyss
        public DbSet<LegacyStatistics> Statistics { get; set; } = default!;

        public DbSet<Banned> BannedList { get; set; } = default!;

        public DbSet<EntityRecord> Records { get; set; } = default!;

        public DbSet<EntityAvatar> Avatars { get; set; } = default!;

        public DbSet<EntitySpiralAbyss> SpiralAbysses { get; set; } = default!;

        public DbSet<EntityFloor> SpiralAbyssFloors { get; set; } = default!;

        public DbSet<EntityDamageRank> DamageRanks { get; set; } = default!;

        public DbSet<EntityTakeDamageRank> TakeDamageRanks { get; set; } = default!;
        #endregion

        #region GachaLog
        public DbSet<GachaStatistics> GachaStatistics { get; set; } = default!;

        public DbSet<InvalidGachaUid> InvalidGachaUids { get; set; } = default!;

        public DbSet<EntityGachaItem> GachaItems { get; set; } = default!;
        #endregion

        #region Achievement WIP
        public DbSet<EntityAchievementArchive> AchievementArchives { get; set; } = default!;

        public DbSet<EntityAchievement> Achievements { get; set; } = default!;
        #endregion

        #region RoleCombat
        public DbSet<RoleCombatRecord> RoleCombatRecords { get; set; } = default!;

        public DbSet<RoleCombatAvatar> RoleCombatAvatars { get; set; } = default!;

        public DbSet<RoleCombatStatistics> RoleCombatStatistics { get; set; } = default!;
        #endregion

        #region Passport
        public DbSet<RefreshToken> RefreshTokens { get; set; } = default!;

        public DbSet<OAuthBindIdentity> OAuthBindIdentities { get; set; } = default!;
        #endregion

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfiguration(new EntityFloorConfiguration());
        }
    }
}