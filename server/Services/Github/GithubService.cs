using server.Models.Github;
using System.Web;

namespace server.Services.Github;

// Scoped
public class GithubService(
    GithubApiService githubApiService,
    PassportService passportService,
    AppOptions appOptions,
    AppDbContext appDbContext,
    TimeProvider timeProvider) : IOAuthProvider
{
    private readonly GithubOptions _githubOptions = appOptions.Github;

    public Task<string> RequestAuthUrlAsync(string state)
    {
        return Task.FromResult($"https://github.com/login/oauth/authorize?client_id={_githubOptions.ClientId}&state={HttpUtility.UrlEncode(state)}");
    }

    public async Task<bool> RefreshTokenAsync(OAuthBindIdentity identity)
    {
        GithubAccessTokenResponse? accessTokenResponse = await githubApiService.GetAccessTokenByRefreshTokenAsync(identity.RefreshToken).ConfigureAwait(false);
        if (accessTokenResponse is null)
        {
            return false;
        }

        identity.RefreshToken = accessTokenResponse.RefreshToken;
        identity.ExpiresAt = (timeProvider.GetLocalNow() + TimeSpan.FromSeconds(accessTokenResponse.RefreshTokenExpiresIn)).ToUnixTimeSeconds();

        await appDbContext.OAuthBindIdentities.UpdateAndSaveAsync(identity).ConfigureAwait(false);
        return true;
    }

    public async ValueTask<OAuthResult> HandleOAuthCallbackAsync(string code, OAuthBindState state)
    {
        GithubAccessTokenResponse? accessTokenResponse = await githubApiService.GetAccessTokenByCodeAsync(code).ConfigureAwait(false);
        if (accessTokenResponse is null)
        {
            return OAuthResult.Fail("Failed to obtain an access token");
        }

        GithubUserResponse? userResponse = await githubApiService.GetUserInfoByAccessTokenAsync(accessTokenResponse.AccessToken).ConfigureAwait(false);
        if (userResponse is null)
        {
            return OAuthResult.Fail("Failed to retrieve GitHub user information");
        }

        OAuthBindIdentity? identity = await appDbContext.OAuthBindIdentities.SingleOrDefaultAsync(b => b.ProviderKind == OAuthProviderKind.Github && b.ProviderId == userResponse.NodeId);
        if (state.UserId is -1)
        {
            // Login mode
            if (identity is null)
            {
                return OAuthResult.Fail("This GitHub account is not linked to a Snap Hutao account");
            }

            return OAuthResult.LoginSuccess(await passportService.CreateTokenResponseAsync(identity.UserId, state.DeviceInfo).ConfigureAwait(false));
        }

        if (identity is not null)
        {
            if (state.UserId != identity.UserId)
            {
                // Already authorized to another user
                return OAuthResult.Fail("This GitHub account is already linked to another Snap Hutao account");
            }

            // Already bound to this user, update access token
            identity.RefreshToken = accessTokenResponse.RefreshToken;
            identity.ExpiresAt = (timeProvider.GetLocalNow() + TimeSpan.FromSeconds(accessTokenResponse.RefreshTokenExpiresIn)).ToUnixTimeSeconds();
            await appDbContext.OAuthBindIdentities.UpdateAndSaveAsync(identity).ConfigureAwait(false);
            return OAuthResult.BindSuccess();
        }

        // First time to bind
        identity = new()
        {
            UserId = state.UserId,
            ProviderKind = OAuthProviderKind.Github,
            ProviderId = userResponse.NodeId,
            DisplayName = userResponse.Login,
            RefreshToken = accessTokenResponse.RefreshToken,
            CreatedAt = timeProvider.GetUnixTimeSeconds(),
            ExpiresAt = (timeProvider.GetLocalNow() + TimeSpan.FromSeconds(accessTokenResponse.RefreshTokenExpiresIn)).ToUnixTimeSeconds(),
        };

        await appDbContext.OAuthBindIdentities.AddAndSaveAsync(identity).ConfigureAwait(false);
        return OAuthResult.BindSuccess();
    }
}
