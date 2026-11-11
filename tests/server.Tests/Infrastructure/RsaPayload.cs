using System.Security.Cryptography;

namespace server.Tests.Infrastructure;

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