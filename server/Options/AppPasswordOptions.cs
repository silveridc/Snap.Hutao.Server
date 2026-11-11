namespace server.Options;

public class AppPasswordOptions
{
    public int RequiredLength { get; set; } = 10;

    public int RequiredUniqueChars { get; set; } = 2;

    public bool RequireDigit { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; } = false;
}
