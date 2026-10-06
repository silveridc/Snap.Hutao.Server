using server.Models.Context;
using server.Models.Entity.Passport;
using server.Models.Response;
using server.Options;
using server.Services.Github;
using server.Services.OAuth;
using System.Security.Cryptography;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(GroupName = "OAuth")]
    public class OAuthController(IServiceProvider serviceProvider) : ControllerBase
    {
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
        private readonly AppOptions appOptions = serviceProvider.GetRequiredService<AppOptions>();

        /// <summary>
        /// 发起第三方 OAuth 授权请求。
        /// </summary>
        /// <param name="kind">OAuth 提供方类型。</param>
        /// <param name="deviceId">设备 ID。</param>
        /// <param name="deviceName">设备名称。</param>
        /// <param name="deviceOs">设备操作系统。</param>
        /// <param name="callbackUri">回调地址。</param>
        /// <returns>重定向到授权页面。</returns>
        [HttpGet("Auth")]
        public async Task<IActionResult> RequestAuthAsync(
            [FromQuery(Name = "provider")] OAuthProviderKind kind,
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

            IOAuthProvider provider = serviceProvider.GetRequiredKeyedService<IOAuthProvider>(kind);
            return Redirect(await provider.RequestAuthUrlAsync(encryptedState).ConfigureAwait(false));
        }

        /// <summary>
        /// 解绑指定类型的 OAuth 账号。
        /// </summary>
        /// <param name="kind">OAuth 提供方类型。</param>
        /// <returns>解绑结果。</returns>
        [Authorize]
        [HttpGet("Unbind")]
        public async Task<IActionResult> UnbindAsync([FromQuery(Name = "provider")] OAuthProviderKind kind)
        {
            int userId = this.GetUserId();
            await this.appDbContext.OAuthBindIdentities.Where(b => b.UserId == userId && b.ProviderKind == kind)
                .ExecuteDeleteAsync()
                .ConfigureAwait(false);

            return Models.Response.Response.Success("Unbound successfully", ServerKeys.ServerOAuthUnbindSucceed);
        }

        /// <summary>
        /// 获取指定类型 OAuth 的绑定状态。
        /// </summary>
        /// <param name="kind">OAuth 提供方类型。</param>
        /// <returns>绑定状态。</returns>
        [Authorize]
        [HttpGet("Status")]
        public async Task<IActionResult> GetBindStatusAsync([FromQuery(Name = "provider")] OAuthProviderKind kind)
        {
            int userId = this.GetUserId();
            OAuthBindIdentity? bindIdentity = await this.appDbContext.OAuthBindIdentities
                .Where(b => b.UserId == userId && b.ProviderKind == kind)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);

            return Response<OAuthBindStatus>.Success("Bind status acquired successfully", ServerKeys.ServerOAuthBindStatusSucceed, bindIdentity is null ? OAuthBindStatus.NotBinded() : OAuthBindStatus.Binded(bindIdentity));
        }

        /// <summary>
        /// 处理 GitHub OAuth 回调并完成绑定或登录。
        /// </summary>
        /// <param name="code">GitHub 返回的授权码。</param>
        /// <param name="state">请求授权时携带的加密状态。</param>
        /// <returns>重定向结果。</returns>
        [HttpGet("Callback/GitHub")]
        public async Task<IActionResult> BindGitHubCallbackAsync(
            [FromQuery(Name = "code")][Required] string code,
            [FromQuery(Name = "state")][Required] string state)
        {
            OAuthBindState? decryptedState = JsonSerializer.Deserialize<OAuthBindState>(DecryptState(state));
            if (decryptedState is null)
            {
                return BadRequest("Invalid state.");
            }

            GithubService githubService = serviceProvider.GetRequiredService<GithubService>();
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
            return this.Redirect($"{decryptedState.CallbackUri}?access_token={result.TokenResponse.AccessToken}&refresh_token={result.TokenResponse.RefreshToken}&expires_in={result.TokenResponse.ExpiresIn}");
        }

        /// <summary>
        /// 加密 OAuth 状态信息。
        /// </summary>
        /// <param name="state">待加密的状态字符串。</param>
        /// <returns>Base64 编码的密文。</returns>
        private string EncryptState(string state)
        {
            using Aes aes = Aes.Create();
            aes.Key = appOptions.OAuthStateEncryptKey;
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
        /// 解密 OAuth 状态信息。
        /// </summary>
        /// <param name="state">Base64 编码的密文。</param>
        /// <returns>解密后的状态字符串。</returns>
        private string DecryptState(string state)
        {
            using Aes aes = Aes.Create();
            aes.Key = appOptions.OAuthStateEncryptKey;
            aes.IV = new byte[16];
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using MemoryStream sourceStream = new(Convert.FromBase64String(state));
            using ICryptoTransform transform = aes.CreateDecryptor();
            using CryptoStream cryptoStream = new(sourceStream, transform, CryptoStreamMode.Read);
            using StreamReader streamReader = new(cryptoStream, leaveOpen: true);
            return streamReader.ReadToEnd();
        }
    }
}