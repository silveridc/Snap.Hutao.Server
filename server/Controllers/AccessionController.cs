using server.Models.OpenSource;

namespace server.Controllers;

[ApiController]
[Route("api/license")]
[ApiExplorerSettings(GroupName = "PublicApi")]
public sealed class AccessionController(AccessionService accessionService) : ControllerBase
{
    /// <summary>
    /// 申请开源许可证.
    /// </summary>
    /// <param name="info">许可证申请信息.</param>
    /// <returns>申请处理结果.</returns>
    [HttpPost("require")]
    public async Task<IActionResult> ApplyOpenSourceLicenseAsync([FromBody] LicenseApplication info)
    {
        return await accessionService.ProcessApplicationAsync(info).ConfigureAwait(false) switch
        {
            ApplicationProcessResult.Ok => Success("Application submitted, please wait for approval", ServerKeys.ServerLicenseApplySucceed),
            ApplicationProcessResult.ReCaptchaVerificationFailed => Fail("Application failed: reCAPTCHA verification failed", ReturnCode.ReCaptchaVerifyFailed, ServerKeys.ServerLicenseApplyReCaptchaFailed),
            ApplicationProcessResult.UsetNotExists => Fail("Application failed: account does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerLicenseApplyUserNotFound),
            ApplicationProcessResult.AlreadyApplied => Fail("Application already submitted; please do not apply again", ReturnCode.AlreadyAppliedForLicense, ServerKeys.ServerLicenseApplyAlreadyApplied),
            _ => throw new InvalidOperationException(),
        };
    }
}
