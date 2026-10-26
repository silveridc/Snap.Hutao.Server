namespace server.Extension;

public static class TimeProviderExtension
{
    public static long GetUnixTimeSeconds(this TimeProvider provider)
    {
        return provider.GetUtcNow().ToUnixTimeSeconds();
    }

    public static long GetUnixTimeMilliseconds(this TimeProvider provider)
    {
        return provider.GetUtcNow().ToUnixTimeMilliseconds();
    }
}
