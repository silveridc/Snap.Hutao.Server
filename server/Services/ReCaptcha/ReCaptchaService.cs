using server.Models.ReCaptcha;

namespace server.Services.ReCaptcha;

//Transient
public sealed class ReCaptchaService(HttpClient httpClient, AppOptions appOptions, IServiceProvider serviceProvider)
{
    private readonly ILogger<ReCaptchaService> _logger = serviceProvider.GetRequiredService<ILogger<ReCaptchaService>>();

    public async Task<ReCaptchaResponse?> VerifyAsync(string token, string? remoteIp = null)
    {
        Dictionary<string, string> parameters = new()
        {
            ["secret"] = appOptions.ReCaptchaKey,
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
            _logger.LogWarning("Recaptcha Verify Error");
        }

        return await message.Content.ReadFromJsonAsync<ReCaptchaResponse>().ConfigureAwait(false);
    }
}
