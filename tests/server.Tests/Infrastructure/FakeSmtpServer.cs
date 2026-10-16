using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace server.Tests.Infrastructure;

/// <summary>
/// 极简 SMTP 服务端，只实现 MailKit 客户端所需的 EHLO / AUTH / MAIL / RCPT / DATA / QUIT 流程。
/// 用于在测试中截获验证码邮件，避免真实发信。
/// </summary>
public sealed partial class FakeSmtpServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource cancellation = new();
    private readonly ConcurrentQueue<string> messages = new();

    public FakeSmtpServer(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>已收到的邮件正文（按接收顺序）。</summary>
    public IReadOnlyCollection<string> Messages => messages;

    /// <summary>等待一封包含验证码的邮件并返回其中的验证码。</summary>
    public async Task<string> WaitForVerifyCodeAsync(TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (string message in messages)
            {
                Match match = VerifyCodePattern().Match(message);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"No verification code mail received within {timeout}. Messages: {messages.Count}");
    }

    public void Clear()
    {
        messages.Clear();
    }

    private async Task AcceptLoopAsync()
    {
        while (!cancellation.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellation.Token);
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using NetworkStream stream = client.GetStream();
                using StreamReader reader = new(stream, Encoding.ASCII, false, 1024, leaveOpen: true);
                using StreamWriter writer = new(stream, Encoding.ASCII, 1024, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };

                await writer.WriteLineAsync("220 fake ESMTP ready");

                string? line;
                while ((line = await reader.ReadLineAsync(cancellation.Token)) is not null)
                {
                    if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250-fake greets you");
                        await writer.WriteLineAsync("250 AUTH PLAIN LOGIN");
                    }
                    else if (line.StartsWith("AUTH PLAIN", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                    }
                    else if (line.StartsWith("AUTH LOGIN", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("334 VXNlcm5hbWU6");
                        _ = await reader.ReadLineAsync(cancellation.Token);
                        await writer.WriteLineAsync("334 UGFzc3dvcmQ6");
                        _ = await reader.ReadLineAsync(cancellation.Token);
                        await writer.WriteLineAsync("235 2.7.0 Authentication successful");
                    }
                    else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase) || line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("250 2.1.0 Ok");
                    }
                    else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");

                        StringBuilder body = new();
                        while ((line = await reader.ReadLineAsync(cancellation.Token)) is not null && line != ".")
                        {
                            body.AppendLine(line);
                        }

                        messages.Enqueue(body.ToString());
                        await writer.WriteLineAsync("250 2.0.0 Ok: queued");
                    }
                    else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 2.0.0 Bye");
                        return;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 2.0.0 Ok");
                    }
                }
            }
            catch
            {
                // 客户端中断，忽略
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        listener.Stop();
        cancellation.Dispose();
    }

    // 验证码形如 <span class="mail-code">XXXXXXXX</span>，MailKit 会做 quoted-printable 编码，故允许中间的软换行
    [GeneratedRegex(@"mail-code[^>]*>(?:=\r?\n)?\s*([A-Z0-9]{6,10})\s*<", RegexOptions.IgnoreCase)]
    private static partial Regex VerifyCodePattern();
}
