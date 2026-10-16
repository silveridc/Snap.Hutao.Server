using System.Net;
using System.Net.Sockets;

namespace server.Tests.Infrastructure;

/// <summary>
/// 测试运行环境的进程级资源：MySQL 测试库、Redis（Garnet）端口、假 SMTP 端口。
/// 所有取值都可以通过环境变量覆盖，便于在 CI 与本地之间切换。
/// </summary>
public static class TestEnvironment
{
    /// <summary>具备建库权限的连接串（默认使用本地 root）。</summary>
    public static string AdminConnectionString { get; } =
        Environment.GetEnvironmentVariable("HUTAO_TEST_MYSQL_ADMIN")
        ?? "Server=127.0.0.1;Uid=root;Pwd=1qaz2wsx;";

    /// <summary>测试库连接串。</summary>
    public static string AppConnectionString { get; } =
        Environment.GetEnvironmentVariable("HUTAO_TEST_MYSQL")
        ?? "Server=127.0.0.1;Database=snaphutao_test;Uid=snaphutao_test;Pwd=1qaz2wsx;Charset=utf8mb4;AllowLoadLocalInfile=true;";

    /// <summary>测试库名（从连接串中解析）。</summary>
    public static string DatabaseName { get; } = ParseDatabase(AppConnectionString);

    /// <summary>假 SMTP 监听端口，MailKit 默认拨号 25。</summary>
    public static int SmtpPort { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("HUTAO_TEST_SMTP_PORT"), out int port) ? port : 25;

    /// <summary>测试库中允许使用的用户名/密码（供建库时授权）。</summary>
    public static (string User, string Password) AppCredentials { get; } = ParseCredentials(AppConnectionString);

    private static string ParseDatabase(string connectionString)
    {
        foreach (string segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = segment.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim().Equals("Database", StringComparison.OrdinalIgnoreCase))
            {
                return pair[1].Trim();
            }
        }

        throw new InvalidOperationException($"Connection string does not contain a Database: {connectionString}");
    }

    private static (string User, string Password) ParseCredentials(string connectionString)
    {
        string user = string.Empty;
        string password = string.Empty;

        foreach (string segment in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] pair = segment.Split('=', 2);
            if (pair.Length != 2)
            {
                continue;
            }

            string key = pair[0].Trim();
            if (key.Equals("Uid", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("User", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Username", StringComparison.OrdinalIgnoreCase))
            {
                user = pair[1].Trim();
            }
            else if (key.Equals("Pwd", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("Password", StringComparison.OrdinalIgnoreCase))
            {
                password = pair[1].Trim();
            }
        }

        return (user, password);
    }

    /// <summary>寻找一个当前空闲的本地端口。</summary>
    public static int FindFreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
