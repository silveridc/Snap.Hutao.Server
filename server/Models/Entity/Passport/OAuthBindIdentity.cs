namespace server.Models.Entity.Passport;

public class OAuthBindIdentity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id
    {
        get; set;
    }

    public int UserId
    {
        get; set;
    }

    [ForeignKey(nameof(UserId))]
    public HutaoUser User { get; set; } = default!;

    public OAuthProviderKind ProviderKind
    {
        get; set;
    }

    [StringLength(256)]
    public string ProviderId { get; set; } = default!;

    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [StringLength(1024)]
    public string RefreshToken { get; set; } = default!;

    public long CreatedAt
    {
        get; set;
    }

    public long ExpiresAt
    {
        get; set;
    }
}
