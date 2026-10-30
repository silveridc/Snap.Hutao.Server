namespace server.Services.OAuth;

public interface IOAuthProvider
{
    public Task<string> RequestAuthUrlAsync(string state);

    // Internal use
    public Task<bool> RefreshTokenAsync(OAuthBindIdentity identity);
}
