using System.Security.Cryptography;
using System.Text;

namespace server.Tests.Infrastructure;

/// <summary>
/// OAuth 授权状态（<c>/OAuth/Auth</c> 产生的 state 参数）在服务端以 AES-CBC 加密，
/// 密钥来自 <c>App:OAuthStateEncryptKey</c>。测试宿主把该密钥固定为 32 字节的
/// <c>i * 7 + 1</c>，这里用同样的算法构造合法 state，以便走通回调分支。
/// </summary>
public static class TestOAuthState
{
    private static readonly byte[] Key = CreateKey();

    /// <summary>加密任意 state 明文。</summary>
    public static string Encrypt(string plainText)
    {
        using Aes aes = Aes.Create();
        aes.Key = Key;
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream targetStream = new();
        using (ICryptoTransform transform = aes.CreateEncryptor())
        {
            using CryptoStream cryptoStream = new(targetStream, transform, CryptoStreamMode.Write);
            using (StreamWriter streamWriter = new(cryptoStream, leaveOpen: true))
            {
                streamWriter.Write(plainText);
                streamWriter.Flush();
            }

            cryptoStream.FlushFinalBlock();
        }

        return Convert.ToBase64String(targetStream.ToArray());
    }

    /// <summary>构造未绑定用户的登录态 state（<c>UserId = -1</c>）。</summary>
    public static string LoginState(string deviceId, string callbackUri)
    {
        return Encrypt($$"""
            {"UserId":-1,"DeviceInfo":{"DeviceId":"{{deviceId}}","DeviceName":"Test Device","OperatingSystem":"Windows"},"CallbackUri":"{{callbackUri}}"}
            """);
    }

    /// <summary>构造绑定到指定用户的绑定态 state。</summary>
    public static string BindState(int userId, string deviceId, string callbackUri)
    {
        return Encrypt($$"""
            {"UserId":{{userId}},"DeviceInfo":{"DeviceId":"{{deviceId}}","DeviceName":"Test Device","OperatingSystem":"Windows"},"CallbackUri":"{{callbackUri}}"}
            """);
    }

    private static byte[] CreateKey()
    {
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)((i * 7 + 1) % 256);
        }

        return key;
    }
}
