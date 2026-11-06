namespace server.Services.Admin;

public static class SystemSettingSecurity
{
    public const string MaskedValue = "********";

    public static bool IsSensitiveKey(string key)
    {
        return key.EndsWith("password", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("secret", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("token", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("privatekey", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("secretaccesskey", StringComparison.OrdinalIgnoreCase)
            || key.Equals(Keys.PizzaHelperUidSalt, StringComparison.OrdinalIgnoreCase)
            || key.Equals(Keys.PizzaHelperApiSalt, StringComparison.OrdinalIgnoreCase)
            || key.Equals(Keys.RecaptchaSecretKey, StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("ReCaptchaKey", StringComparison.OrdinalIgnoreCase);
    }
}
