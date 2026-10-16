using System.Security.Cryptography;

namespace server.Tests.Infrastructure;

/// <summary>
/// 客户端提交 UserName / Password 等字段时使用 RSA-OAEP-SHA1 加密，
/// 服务端用 server/privkey.key 解密。这里从同一私钥导出公钥来构造请求体。
/// </summary>
public static class RsaPayload
{
    private static readonly RSA Rsa = CreateRsa();

    public static string Encrypt(string plainText)
    {
        byte[] cipher = Rsa.Encrypt(System.Text.Encoding.UTF8.GetBytes(plainText), RSAEncryptionPadding.OaepSHA1);
        return Convert.ToBase64String(cipher);
    }

    private static RSA CreateRsa()
    {
        RSA rsa = RSA.Create(2048);
        rsa.ImportFromPem(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "privkey.key")));
        return rsa;
    }
}
