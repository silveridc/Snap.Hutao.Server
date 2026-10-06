namespace server.Models.Admin
{
    public sealed class BanItem
    {
        public string Uid { get; set; } = default!;

        public string? Reason { get; set; }

        public long BanTime { get; set; }
    }

    public sealed class RedeemCodeItem
    {
        public uint Id { get; set; }

        public string Code { get; set; } = default!;

        public uint Type { get; set; }

        public uint ServiceType { get; set; }

        public int Value { get; set; }

        public string Description { get; set; } = default!;

        public string Creator { get; set; } = default!;

        public DateTimeOffset CreateTime { get; set; }

        public DateTimeOffset ExpireTime { get; set; }

        public uint TimesAllowed { get; set; }

        public int UsedCount { get; set; }
    }

    public sealed class LicenseApplicationItem
    {
        public long PrimaryId { get; set; }

        public int UserId { get; set; }

        public string? UserName { get; set; }

        public string ProjectUrl { get; set; } = default!;

        public string ApprovalCode { get; set; } = default!;

        public bool IsApproved { get; set; }
    }

    public sealed class RedeemCodeUpdateRequest
    {
        public uint Id { get; set; }

        public uint Type { get; set; }

        public uint ServiceType { get; set; }

        public int Value { get; set; }

        public string Description { get; set; } = default!;

        public DateTimeOffset ExpireTime { get; set; }

        public uint TimesAllowed { get; set; }
    }

    public sealed class AnnouncementUpdateRequest
    {
        public long Id { get; set; }

        public string Locale { get; set; } = "ALL";

        public string Title { get; set; } = default!;

        public string Content { get; set; } = default!;

        public int Severity { get; set; }

        public string Link { get; set; } = default!;

        public string? MaxPresentVersion { get; set; }
    }

    public sealed class AuditLogItem
    {
        public long Id { get; set; }

        public int AdminUserId { get; set; }

        public string? AdminUserName { get; set; }

        public string Action { get; set; } = default!;

        public string? Detail { get; set; }

        public bool Success { get; set; }

        public long Timestamp { get; set; }
    }

    public sealed class UserGachaUid
    {
        public string Uid { get; set; } = default!;

        public int ItemCount { get; set; }
    }

    public sealed class UserDetail
    {
        public int Id { get; set; }

        public string? UserName { get; set; }

        public bool IsMaintainer { get; set; }

        public bool IsLicensedDeveloper { get; set; }

        public long GachaLogExpireAt { get; set; }

        public long CdnExpireAt { get; set; }

        public int RedeemUsedCount { get; set; }

        public List<UserGachaUid> GachaUids { get; set; } = [];
    }

    public sealed class RankEntry
    {
        public string Uid { get; set; } = default!;

        public double Value { get; set; }
    }

    public sealed class BatchUidRequest
    {
        public List<string> Uids { get; set; } = [];

        public string? Reason { get; set; }
    }

    public sealed class BatchIdRequest
    {
        public List<uint> Ids { get; set; } = [];
    }

    public sealed class HutaoReleasePublishRequest
    {
        public string Version { get; set; } = default!;

        public string Validation { get; set; } = default!;

        public List<global::server.Models.Patch.HutaoPackageMirror> Mirrors { get; set; } = [];

        public string? ReleaseNotes { get; set; }
    }

    public sealed class HutaoReleaseItem
    {
        public long Id { get; set; }

        public string Version { get; set; } = default!;

        public string Validation { get; set; } = default!;

        public List<global::server.Models.Patch.HutaoPackageMirror> Mirrors { get; set; } = [];

        public string? ReleaseNotes { get; set; }

        public bool IsActive { get; set; }

        public DateTimeOffset PublishedAt { get; set; }

        public int PublishedBy { get; set; }

        public string? PublishedByUserName { get; set; }
    }

    public sealed class HutaoReleaseActivateRequest
    {
        public long Id { get; set; }
    }
}
