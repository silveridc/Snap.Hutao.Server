namespace server.Models.Entity.Passport
{
    [Table("passport_verifications")]
    public class PassportVerification
    {
        [Key]
        public string NormalizedUserName { get; set; } = default!;

        public string VerifyCode { get; set; } = default!;

        public long GeneratedTimestamp { get; set; } = default!;

        public long ExpireTimestamp { get; set; } = default!;
    }
}