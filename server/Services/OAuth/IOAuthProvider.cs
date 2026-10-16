namespace server.Services.OAuth
{
    public interface IOAuthProvider
    {
        Task<string> RequestAuthUrlAsync(string state);

        // Internal use
        Task<bool> RefreshTokenAsync(OAuthBindIdentity identity);
    }
}