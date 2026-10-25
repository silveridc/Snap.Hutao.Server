namespace server.Extension;

/// <summary>
/// <see cref="TimeProvider"/> 的 Unix 时间戳便捷读取。
/// 全服务的时间统一从注入的 <see cref="TimeProvider"/> 取（测试可换 Fake），不再各自
/// <c>DateTimeOffset.UtcNow.ToUnixTimeSeconds()</c>；纯静态上下文用 <c>TimeProvider.System</c>。
/// </summary>
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
