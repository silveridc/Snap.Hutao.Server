namespace server.Models.Passport
{
    public class UserInfo
    {
        public string? NormalizedUserName { get; set; }

        public string? UserName { get; set; }

        public bool IsLicensedDeveloper { get; set; }

        public bool IsMaintainer { get; set; }

        public DateTimeOffset GachaLogExpireAt { get; set; }

        public DateTimeOffset CdnExpireAt { get; set; }
    }
}