namespace server.Options;

public class EncryptOptions
{
    public string RSAPrivateKey { get; set; } = Path.Combine(Program.AppResourcesPath, "privkey.key");

    public byte[] OAuthStateEncrypt { get; set; } = default!;
}
