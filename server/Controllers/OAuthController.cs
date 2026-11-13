using System.Security.Cryptography;

namespace server.Controllers;

[ApiController]
[Route("api/oauth")]
[ApiExplorerSettings(GroupName = "OAuth")]
public class OAuthController(IServiceProvider serviceProvider, AppDbContext appDbContext, AppOptions appOptions, GithubService githubService) : ControllerBase
{
    /// <summary>
    /// Starts a third-party OAuth authorization request.
    /// </summary>
    /// <param name="provider">The OAuth provider kind.</param>
    /// <param name="deviceId">The device ID.</param>
    /// <param name="deviceName">The device name.</param>
    /// <param name="deviceOs">The device operating system.</param>
    /// <param name="callbackUri">The callback URI.</param>
    /// <returns>A redirect to the authorization page.</returns>
    [HttpGet("authorizations/{provider}")]
    public async Task<IActionResult> RequestAuthAsync(
        [FromRoute] OAuthProviderKind provider,
        [FromQuery(Name = "device_id")][Required] string deviceId,
        [FromQuery(Name = "device_name")][Required(AllowEmptyStrings = true)] string deviceName,
        [FromQuery(Name = "device_os")][Required(AllowEmptyStrings = true)] string deviceOs,
        [FromQuery(Name = "callback")][Required] string callbackUri)
    {
        OAuthBindState state;
        if (this.TryGetUserId(out int userId))
        {
            state = new(userId, deviceId, deviceName, deviceOs, callbackUri);
        }
        else
        {
            state = new(deviceId, deviceName, deviceOs, callbackUri);
        }

        string encryptedState = EncryptState(JsonSerializer.Serialize(state));

        IOAuthProvider oauthProvider = serviceProvider.GetRequiredKeyedService<IOAuthProvider>(provider);
        return Redirect(await oauthProvider.RequestAuthUrlAsync(encryptedState).ConfigureAwait(false));
    }

    /// <summary>
    /// Unbinds the OAuth account of the specified provider.
    /// </summary>
    /// <param name="provider">The OAuth provider kind.</param>
    /// <returns>The unbind result.</returns>
    [Authorize]
    [HttpDelete("bindings/{provider}")]
    public async Task<IActionResult> UnbindAsync([FromRoute] OAuthProviderKind provider)
    {
        int userId = this.GetUserId();
        await appDbContext.OAuthBindIdentities.Where(b => b.UserId == userId && b.ProviderKind == provider)
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);

        return Success("Unbound successfully", ServerKeys.ServerOAuthUnbindSucceed);
    }

    /// <summary>
    /// Gets the binding status of the specified OAuth provider.
    /// </summary>
    /// <param name="provider">The OAuth provider kind.</param>
    /// <returns>The binding status.</returns>
    [Authorize]
    [HttpGet("bindings/{provider}")]
    public async Task<IActionResult> GetBindStatusAsync([FromRoute] OAuthProviderKind provider)
    {
        int userId = this.GetUserId();
        OAuthBindIdentity? bindIdentity = await appDbContext.OAuthBindIdentities
            .Where(b => b.UserId == userId && b.ProviderKind == provider)
            .SingleOrDefaultAsync()
            .ConfigureAwait(false);

        return Success("Bind status acquired successfully", bindIdentity is null ? OAuthBindStatus.NotBinded() : OAuthBindStatus.Binded(bindIdentity), ServerKeys.ServerOAuthBindStatusSucceed);
    }

    /// <summary>
    /// Handles the GitHub OAuth callback and completes the binding or sign-in.
    /// </summary>
    /// <param name="code">The authorization code returned by GitHub.</param>
    /// <param name="state">The encrypted state carried by the authorization request.</param>
    /// <returns>The redirect result.</returns>
    [HttpGet("~/api/OAuth/Callback/GitHub")]
    public async Task<IActionResult> BindGitHubCallbackAsync(
        [FromQuery(Name = "code")][Required] string code,
        [FromQuery(Name = "state")][Required] string state)
    {
        string? plainState = DecryptState(state);
        if (plainState is null)
        {
            return BadRequest("Invalid state.");
        }

        OAuthBindState? decryptedState = JsonSerializer.Deserialize<OAuthBindState>(plainState);
        if (decryptedState is null)
        {
            return BadRequest("Invalid state.");
        }

        OAuthResult result = await githubService.HandleOAuthCallbackAsync(code, decryptedState).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        if (result.IsBind)
        {
            return Redirect(decryptedState.CallbackUri);
        }

        ArgumentNullException.ThrowIfNull(result.TokenResponse);
        return Redirect($"{decryptedState.CallbackUri}?access_token={result.TokenResponse.AccessToken}&refresh_token={result.TokenResponse.RefreshToken}&expires_in={result.TokenResponse.ExpiresIn}");
    }

    /// <summary>
    /// Encrypts the OAuth state information.
    /// </summary>
    /// <param name="state">The state string to encrypt.</param>
    /// <returns>The Base64-encoded ciphertext.</returns>
    private string EncryptState(string state)
    {
        using Aes aes = Aes.Create();
        aes.Key = appOptions.Encrypt.OAuthStateEncrypt;
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using MemoryStream targetStream = new();
        using (ICryptoTransform transform = aes.CreateEncryptor())
        {
            using CryptoStream cryptoStream = new(targetStream, transform, CryptoStreamMode.Write);
            using (StreamWriter streamWriter = new(cryptoStream, leaveOpen: true))
            {
                streamWriter.Write(state);
                streamWriter.Flush();
            }

            cryptoStream.FlushFinalBlock();
        }

        return Convert.ToBase64String(targetStream.ToArray());
    }

    /// <summary>
    /// Decrypts the OAuth state information.
    /// </summary>
    /// <param name="state">The Base64-encoded ciphertext.</param>
    /// <returns>The decrypted state string, or <see langword="null"/> when the ciphertext is forged or corrupted.</returns>
    private string? DecryptState(string state)
    {
        try
        {
            using Aes aes = Aes.Create();
            aes.Key = appOptions.Encrypt.OAuthStateEncrypt;
            aes.IV = new byte[16];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using MemoryStream sourceStream = new(Convert.FromBase64String(state));
            using ICryptoTransform transform = aes.CreateDecryptor();
            using CryptoStream cryptoStream = new(sourceStream, transform, CryptoStreamMode.Read);
            using StreamReader streamReader = new(cryptoStream, leaveOpen: true);
            return streamReader.ReadToEnd();
        }
        catch (FormatException)
        {
            // 不是合法 Base64
            return null;
        }
        catch (CryptographicException)
        {
            // 长度不是分组的整数倍，或填充不合法（非本密钥的密文）
            return null;
        }
    }
}
