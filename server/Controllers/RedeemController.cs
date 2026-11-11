using server.Models.Entity.Redeem;
using server.Models.Redeem;

namespace server.Controllers;

[ApiController]
[Route("api/redeem-codes")]
[ApiExplorerSettings(GroupName = "Redeem")]
public class RedeemController(RedeemService redeemService, AppDbContext appDbContext) : ControllerBase
{
    /// <summary>
    /// Redeems a redeem code.
    /// </summary>
    /// <param name="req">The redemption request.</param>
    /// <returns>The redemption result.</returns>
    [HttpPost("redemptions")]
    public async Task<IActionResult> UseRedeemCodeAsync([FromBody] RedeemUseRequest req)
    {
        if (req.Username is null)
        {
            if (await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false) is not { } user)
            {
                return Fail("Please log in or register a Snap Hutao account first", ReturnCode.NoUserIdentity, ServerKeys.ServerPassportLoginRequired);
            }

            req.Username = user.UserName;
        }

        RedeemUseResponse response = await redeemService.UseRedeemCodeAsync(req).ConfigureAwait(false);

        return response.Status switch
        {
            RedeemStatus.Ok => Success<RedeemUseResponse>($"Redeemed successfully, granted {response.Value} days of {(response.Type == RedeemCodeTargetServiceType.GachaLog ? "gacha log" : "CDN")} service, expires at: {response.ExpireTime:yyyy/M/d hh:mm:ss}", response),
            RedeemStatus.Invalid => Fail("Redeem code is invalid", ReturnCode.RedeemCodeInvalid, ServerKeys.ServerRedeemCodeInvalid),
            RedeemStatus.NotExists => Fail("Redeem code does not exist", ReturnCode.RedeemCodeNotExists, ServerKeys.ServerRedeemCodeNotFound),
            RedeemStatus.AlreadyUsed => Fail("Redeem code has already been used", ReturnCode.RedeemCodeAlreadyUsed, ServerKeys.ServerRedeemCodeAlreadyUsed),
            RedeemStatus.Expired => Fail("Redeem code has expired", ReturnCode.RedeemCodeExpired, ServerKeys.ServerRedeemCodeExpired),
            RedeemStatus.NotEnough => Fail("Redeem code has no remaining uses", ReturnCode.RedeemCodeNotEnough, ServerKeys.ServerRedeemCodeNotEnough),
            RedeemStatus.NoSuchUser => Fail("User does not exist", ReturnCode.RedeemCodeNoSuchUser, ServerKeys.ServerRedeemNoSuchUser),
            RedeemStatus.DbError => Fail("Database error", ReturnCode.RedeemCodeDbException, ServerKeys.ServerDatabaseError),
            _ => throw new NotSupportedException(),
        };
    }
}
