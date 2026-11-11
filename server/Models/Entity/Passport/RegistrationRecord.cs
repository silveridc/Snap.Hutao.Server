namespace server.Models.Entity.Passport;

[Table("registration_records")]
public sealed class RegistrationRecord
{
    [Key]
    [StringLength(256)]
    public string UserName { get; set; } = default!;
}
