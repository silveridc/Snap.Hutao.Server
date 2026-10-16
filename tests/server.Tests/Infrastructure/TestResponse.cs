using System.Text.Json;

namespace server.Tests.Infrastructure;

/// <summary>服务端统一响应外壳（Models/Response/Response.cs）的测试侧镜像。</summary>
public sealed class TestResponse
{
    public int Retcode { get; init; }

    public string? Message { get; init; }

    public string? L10nKey { get; init; }

    public JsonElement Data { get; init; }

    public bool HasData { get; init; }

    public static TestResponse Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (root.ValueKind is not JsonValueKind.Object || !root.TryGetProperty("retcode", out JsonElement retcode))
        {
            throw new InvalidOperationException($"Response is not a Snap Hutao envelope: {json}");
        }

        JsonElement data = default;
        bool hasData = root.TryGetProperty("data", out data);

        return new()
        {
            Retcode = retcode.GetInt32(),
            Message = root.TryGetProperty("message", out JsonElement message) ? message.GetString() : null,
            L10nKey = root.TryGetProperty("l10nKey", out JsonElement key) ? key.GetString() : null,
            Data = hasData ? data.Clone() : default,
            HasData = hasData,
        };
    }

    public T? DeserializeData<T>()
    {
        return HasData && Data.ValueKind is not JsonValueKind.Null ? Data.Deserialize<T>() : default;
    }
}

/// <summary>响应读取扩展。</summary>
public static class HttpResponseMessageExtension
{
    public static async Task<TestResponse> ReadTestResponseAsync(this HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        return TestResponse.Parse(body);
    }
}

/// <summary>服务端返回码（Models/Response/ReturnCode.cs）中测试会用到的取值。</summary>
public static class ReturnCodes
{
    public const int Success = 0;
    public const int NotFound = 511000;
    public const int InvalidUploadData = 511001;
    public const int InvalidQueryString = 511002;
    public const int InvalidRequestBody = 511003;
    public const int BannedUid = 512001;
    public const int NotCurrentSchedule = 512002;
    public const int GachaLogServiceNotAllowed = 513001;
    public const int InvalidGachaLogItems = 513002;
    public const int RegisterFail = 514001;
    public const int LoginFail = 514002;
    public const int CancelFail = 514003;
    public const int NoUserIdentity = 514008;
    public const int InvalidUserName = 514009;
    public const int TooShortPassword = 514010;
    public const int ReCaptchaVerifyFailed = 515001;
    public const int ServiceKeyInvalid = 516001;
    public const int UserNameNotExists = 516002;
    public const int RedeemCodeInvalid = 518001;
    public const int RedeemCodeNotExists = 518002;
    public const int RedeemCodeAlreadyUsed = 518003;
    public const int RedeemCodeExpired = 518004;
    public const int RedeemCodeNotEnough = 518005;
    public const int RedeemCodeNoSuchUser = 518006;
    public const int VerifyCodeTooFrequently = 524001;
    public const int TooManyGachaLogUids = 533001;
    public const int PreviousRequestNotCompleted = 541001;
    public const int ComputingStatistics = 542001;
    public const int VerifyCodeNotAllowed = 544001;
    public const int AlreadyAppliedForLicense = 545001;
    public const int GachaLogExtendDbException = 553002;
    public const int LicenseApprovalFailed = 555001;
    public const int CdnExtendDbException = 557001;
}
