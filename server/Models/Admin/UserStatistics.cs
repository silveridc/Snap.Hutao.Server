namespace server.Models.Admin
{
    public sealed class UserStatistics
    {
        public int TotalUsers { get; set; }

        public int Maintainers { get; set; }

        public int LicensedDevelopers { get; set; }

        public int ActiveGachaLogUsers { get; set; }

        public int ActiveCdnUsers { get; set; }

        public int BannedUids { get; set; }

        public long GachaItemCount { get; set; }

        public int SpiralAbyssRecordCount { get; set; }

        public int RoleCombatRecordCount { get; set; }
    }

    public sealed class UserListItem
    {
        public int Id { get; set; }

        public string? UserName { get; set; }

        public bool IsMaintainer { get; set; }

        public bool IsLicensedDeveloper { get; set; }

        public long GachaLogExpireAt { get; set; }

        public long CdnExpireAt { get; set; }
    }

    public sealed class PagedResult<T>
    {
        public long Total { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public List<T> Items { get; set; } = [];
    }

    public sealed class UserPrivilegeRequest
    {
        public int UserId { get; set; }

        public bool? IsMaintainer { get; set; }

        public bool? IsLicensedDeveloper { get; set; }
    }

    public sealed class UserResetPasswordRequest
    {
        public int UserId { get; set; }

        public string NewPassword { get; set; } = default!;
    }
}
