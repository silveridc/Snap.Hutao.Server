namespace server.Models.Entity.Passport;

public class RefreshToken
{
    [Key]
    public int Id
    {
        get; set;
    }

    public int UserId
    {
        get; set;
    }

    [Required]
    [StringLength(500)]
    public string Token { get; set; } = default!;

    public DateTimeOffset ExpiresAt
    {
        get; set;
    }

    public DateTimeOffset CreatedAt
    {
        get; set;
    }

    [StringLength(64)]
    public string DeviceId { get; set; } = default!;

    [StringLength(128)]
    public string? DeviceName
    {
        get; set;
    }

    [StringLength(64)]
    public string? OperatingSystem
    {
        get; set;
    }

    [ForeignKey(nameof(UserId))]
    public HutaoUser User { get; set; } = default!;
}
