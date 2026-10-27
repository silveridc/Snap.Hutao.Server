namespace server.Models.Admin;

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

/// <summary>某张兑换码的一条使用记录。</summary>
public sealed class RedeemCodeUsageItem
{
    public int UserId { get; set; }

    public string? UserName { get; set; }

    public DateTimeOffset UseTime { get; set; }
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

    /// <summary>后台专属账号（SSO）身份；主站维护者账号执行时为空。</summary>
    public int? AdminAccountId { get; set; }

    public string? AdminUserName { get; set; }

    public string Action { get; set; } = default!;

    public string? Detail { get; set; }

    /// <summary>操作来源 IP。</summary>
    public string? Ip { get; set; }

    /// <summary>IP 属地，IP2Region 的四段：国家/省份/城市/运营商，缺段为 "0"；按 IP 现查，查不到为 null。</summary>
    public string?[]? IpRegion { get; set; }

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

    /// <summary>最低强制版本：客户端低于它时强制更新。可空，且不得大于 <see cref="Version"/>。</summary>
    public string? MinimumVersion { get; set; }

    public List<global::server.Models.Patch.HutaoPackageMirror> Mirrors { get; set; } = [];

    public string? ReleaseNotes { get; set; }
}

public sealed class HutaoReleaseItem
{
    public long Id { get; set; }

    public string Version { get; set; } = default!;

    public string Validation { get; set; } = default!;

    public string? MinimumVersion { get; set; }

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

/// <summary>补丁存储桶中的一个对象。</summary>
public sealed class ReleaseBucketObjectItem
{
    public string Key { get; set; } = default!;

    public long Size { get; set; }

    public DateTimeOffset LastModified { get; set; }
}

/// <summary>补丁存储桶的对象清单，供后台选择要发布的安装包。</summary>
public sealed class ReleaseBucketListResult
{
    public string Host { get; set; } = default!;

    public string Prefix { get; set; } = default!;

    public bool Truncated { get; set; }

    public List<ReleaseBucketObjectItem> Objects { get; set; } = [];
}

/// <summary>从存储桶发布：只需给出对象键，校验值与下载地址由服务端补齐。</summary>
public sealed class HutaoReleasePublishFromBucketRequest
{
    public string Key { get; set; } = default!;

    /// <summary>留空时从对象键的文件名里解析四段版本号。</summary>
    public string? Version { get; set; }

    /// <summary>镜像名称，留空用存储桶主机名。</summary>
    public string? MirrorName { get; set; }

    public string? MinimumVersion { get; set; }

    public string? ReleaseNotes { get; set; }
}
