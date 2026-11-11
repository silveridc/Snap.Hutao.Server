namespace server.Extension;

internal static class AppOptionsExtension
{
    public static SymmetricSecurityKey GetJwtSecurityKey(this AppOptions options)
    {
        return new(Encoding.UTF8.GetBytes(options.Jwt.Raw));
    }
}
