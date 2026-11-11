using server.Models.OpenSource;

namespace server.Controllers;

[ApiController]
[Route("api/license")]
[ApiExplorerSettings(GroupName = "PublicApi")]
public sealed class AccessionController(AccessionService accessionService) : ControllerBase
{
    /// <summary>
    /// Applies for an open-source license.
    /// </summary>
    /// <param name="info">The license application information.</param>
    /// <returns>The application processing result.</returns>
    [HttpPost("require")]
    public async Task<IActionResult> ApplyOpenSourceLicenseAsync([FromBody] LicenseApplication info) =>
        await accessionService.ProcessApplicationAsync(info).ConfigureAwait(false) switch
        {
            ApplicationProcessResult.Ok => Success("Application submitted, please wait for approval", ServerKeys.ServerLicenseApplySucceed),
            ApplicationProcessResult.ReCaptchaVerificationFailed => Fail("Application failed: reCAPTCHA verification failed", ReturnCode.ReCaptchaVerifyFailed, ServerKeys.ServerLicenseApplyReCaptchaFailed),
            ApplicationProcessResult.UsetNotExists => Fail("Application failed: account does not exist", ReturnCode.UserNameNotExists, ServerKeys.ServerLicenseApplyUserNotFound),
            ApplicationProcessResult.AlreadyApplied => Fail("Application already submitted; please do not apply again", ReturnCode.AlreadyAppliedForLicense, ServerKeys.ServerLicenseApplyAlreadyApplied),
            _ => throw new InvalidOperationException(),
        };
}
