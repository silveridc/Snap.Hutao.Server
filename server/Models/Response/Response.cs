namespace server.Models.Response
{
    public class Response(ReturnCode code, string message)
    {
        [JsonPropertyName("retcode")]
        public ReturnCode Code { get; set; } = code;

        [JsonPropertyName("message")]
        public string Message { get; set; } = message;

        [JsonPropertyName("l10nKey")]
        public string? LocalizationKey { get; set; }

        public static IActionResult Success(string msg)
        {
            return new JsonResult(new Response(ReturnCode.Success, msg));
        }
        public static IActionResult ApiSuccess(int status, string message, object data)
        {
            return new JsonResult(new { status, message, data })
            {
                StatusCode = status
            };
        }
        public static IActionResult Success(string msg, string key)
        {
            return new JsonResult(new Response(ReturnCode.Success, msg) { LocalizationKey = key });
        }

        public static IActionResult Fail(ReturnCode code, string msg)
        {
            return new JsonResult(new Response(code, msg));
        }

        public static IActionResult Fail(ReturnCode code, string msg, string key)
        {
            return new JsonResult(new Response(code, msg) { LocalizationKey = key });
        }

        public static Response FailResponse(ReturnCode code, string msg, string key)
        {
            return new(code, msg) { LocalizationKey = key };
        }
    }

    public class Response<T>(ReturnCode code, string message, T data) : Response(code, message)
    {
        [JsonPropertyName("data")]
        public T? Data { get; set; } = data;

        public static IActionResult Success(string msg, T data)
        {
            return new JsonResult(new Response<T>(ReturnCode.Success, msg, data));
        }

        public static IActionResult Success(string msg, string key, T data)
        {
            return new JsonResult(new Response<T>(ReturnCode.Success, msg, data) { LocalizationKey = key });
        }
    }
}