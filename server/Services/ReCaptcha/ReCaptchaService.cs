using server.Models.ReCaptcha;
using server.Services.Admin;

namespace server.Services.ReCaptcha;

// Transient
public sealed class ReCaptchaService(HttpClient httpClient, SystemConfigService systemConfigService, ILogger<ReCaptchaService> logger)
{
    public async Task<ReCaptchaResponse?> VerifyAsync(string token, string? remoteIp = null)
    {
        Dictionary<string, string> parameters = new()
        {
            ["secret"] = systemConfigService.RecaptchaSecretKey,
            ["response"] = token,
        };

        if (!string.IsNullOrWhiteSpace(remoteIp))
        {
            parameters["remoteip"] = remoteIp;
        }

        using FormUrlEncodedContent content = new(parameters);
        using HttpResponseMessage message = await httpClient.PostAsync("https://www.recaptcha.net/recaptcha/api/siteverify", content).ConfigureAwait(false);

        if (!message.IsSuccessStatusCode)
        {
            logger.LogWarning("Recaptcha Verify Error");
        }

        return await message.Content.ReadFromJsonAsync<ReCaptchaResponse>().ConfigureAwait(false);
    }
}
