namespace server.Models.Entity.Passport
{
    [Table("registration_records")]
    public sealed class RegistrationRecord
    {
        [Key]
        public string UserName { get; set; } = default!;
    }
}