using System.Security.Cryptography;

namespace server.Tests.Infrastructure;

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
