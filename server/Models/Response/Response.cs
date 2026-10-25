namespace server.Models.Response;

/// <summary>
/// 统一响应外壳。字段与启动器/后台的既有契约一致：<c>retcode</c>、<c>message</c>、
/// <c>l10nKey</c>（空值省略）、<c>data</c>。序列化形态不得改动——
/// 启动器按 <c>retcode</c> 判定成败，后台按 <c>l10nKey</c> 本地化消息。
/// </summary>
/// <typeparam name="T"><c>data</c> 字段的类型。</typeparam>
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

/// <summary>
/// 响应外壳的工厂。所有控制器经 <c>global using static</c> 直接裸调
/// <c>Success(...)</c>/<c>Fail(...)</c>——控制器里 <c>Response</c> 与
/// <c>ControllerBase.Response</c> 属性撞名，限定名永远绕不开，静态导入一劳永逸。
/// </summary>
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
