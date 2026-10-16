using server.Models.ReCaptcha;

namespace server.Services.ReCaptcha
{
    // Transient
    public sealed class ReCaptchaService(HttpClient httpClient, AppOptions appOptions)
    {
        public async Task<ReCaptchaResponse?> VerifyAsync(string token)
        {
            string url = $"https://www.google.com/recaptcha/api/siteverify?secret={appOptions.ReCaptchaKey}&response={token}";
            HttpResponseMessage message = await httpClient.PostAsync(url, null).ConfigureAwait(false);
            return await message.Content.ReadFromJsonAsync<ReCaptchaResponse>().ConfigureAwait(false);
        }
    }
}