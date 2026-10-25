using server.Models.Entity.Redeem;
using server.Models.Redeem;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(GroupName = "Redeem")]
public class RedeemController(IServiceProvider serviceProvider) : ControllerBase
{
    private readonly RedeemService _redeemService = serviceProvider.GetRequiredService<RedeemService>();
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();

    /// <summary>
    /// 使用兑换码。
    /// </summary>
    /// <param name="req">兑换请求。</param>
    /// <returns>兑换结果。</returns>
    [HttpPost("Use")]
    public async Task<IActionResult> UseRedeemCodeAsync([FromBody] RedeemUseRequest req)
    {
        if (req.Username is null)
        {
            if (await this.GetUserAsync(_appDbContext.Users).ConfigureAwait(false) is not { } user)
            {
                return Fail("Please log in or register a Snap Hutao account first", ReturnCode.NoUserIdentity, ServerKeys.ServerPassportLoginRequired);
            }

            req.Username = user.UserName;
        }

        RedeemUseResponse response = await _redeemService.UseRedeemCodeAsync(req).ConfigureAwait(false);

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
