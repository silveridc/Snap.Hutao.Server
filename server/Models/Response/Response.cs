namespace server.Models.Response;

public class Response<T>(ReturnCode code, string message, T? data = default)
{
    [JsonPropertyName("retcode")]
    public ReturnCode Code { get; set; } = code;

    [JsonPropertyName("message")]
    public string Message { get; set; } = message;

    [JsonPropertyName("l10nKey")]
    public string? LocalizationKey { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; } = data;
}

public static class Response
{
    public static Response<T> CreateSuccess<T>(string message, T? data = default, string? key = null)
    {
        return new(ReturnCode.Success, message, data) { LocalizationKey = key };
    }

    public static IActionResult Success<T>(string message, T? data = default, string? key = null)
    {
        return new JsonResult(CreateSuccess(message, data, key));
    }

    public static IActionResult Success(string message, string? key = null)
    {
        return new JsonResult(CreateSuccess<object?>(message, null, key));
    }

    public static Response<T> CreateFail<T>(string message, ReturnCode code, T? data = default, string? key = null)
    {
        return new(code, message, data) { LocalizationKey = key };
    }

    public static IActionResult Fail<T>(string message, ReturnCode code, T? data = default, string? key = null)
    {
        return new JsonResult(CreateFail(message, code, data, key));
    }

    public static IActionResult Fail(string message, ReturnCode code, string? key = null)
    {
        return new JsonResult(CreateFail<object?>(message, code, null, key));
    }
}
