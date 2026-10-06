using server.Models.OpenSource;
using server.Models.Response;
using server.Services.Licensing;

namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class AccessionController(AccessionService accessionService) : ControllerBase
{
    /// <summary>
    /// 申请开源许可证。
    /// </summary>
    /// <param name="info">许可证申请信息。</param>
    /// <returns>申请处理结果。</returns>
    [HttpPost("ApplyOpenSourceLicense")]
    public async Task<IActionResult> ApplyOpenSourceLicenseAsync([FromBody] LicenseApplication info)
    {
        return await accessionService.ProcessApplicationAsync(info).ConfigureAwait(false) switch
        {
            ApplicationProcessResult.Ok => Models.Response.Response.Success("Application submitted, please wait for approval", ServerKeys.ServerLicenseApplySucceed),
            ApplicationProcessResult.ReCaptchaVerificationFailed => Models.Response.Response.Fail(ReturnCode.ReCaptchaVerifyFailed, "Application failed: reCAPTCHA verification failed", ServerKeys.ServerLicenseApplyReCaptchaFailed),
            ApplicationProcessResult.UsetNotExists => Models.Response.Response.Fail(ReturnCode.UserNameNotExists, "Application failed: account does not exist", ServerKeys.ServerLicenseApplyUserNotFound),
            ApplicationProcessResult.AlreadyApplied => Models.Response.Response.Fail(ReturnCode.AlreadyAppliedForLicense, "Application already submitted; please do not apply again", ServerKeys.ServerLicenseApplyAlreadyApplied),
            _ => throw new InvalidOperationException(),
        };
    }
}