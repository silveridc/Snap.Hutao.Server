using server.Models.Entity.Redeem;
using server.Models.Redeem;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(GroupName = "Redeem")]
    public class RedeemController(IServiceProvider serviceProvider) : ControllerBase
    {
        private readonly RedeemService redeemService = serviceProvider.GetRequiredService<RedeemService>();
        private readonly AppDbContext appDbContext = serviceProvider.GetRequiredService<AppDbContext>();

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
                if (await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false) is not { } user)
                {
                    return Models.Response.Response.Fail(ReturnCode.NoUserIdentity, "Please log in or register a Snap Hutao account first", ServerKeys.ServerPassportLoginRequired);
                }

                req.Username = user.UserName;
            }

            RedeemUseResponse response = await redeemService.UseRedeemCodeAsync(req).ConfigureAwait(false);

            return response.Status switch
            {
                RedeemStatus.Ok => Response<RedeemUseResponse>.Success($"Redeemed successfully, granted {response.Value} days of {(response.Type == RedeemCodeTargetServiceType.GachaLog ? "gacha log" : "CDN")} service, expires at: {response.ExpireTime:yyyy/M/d hh:mm:ss}", response),
                RedeemStatus.Invalid => Models.Response.Response.Fail(ReturnCode.RedeemCodeInvalid, "Redeem code is invalid", ServerKeys.ServerRedeemCodeInvalid),
                RedeemStatus.NotExists => Models.Response.Response.Fail(ReturnCode.RedeemCodeNotExists, "Redeem code does not exist", ServerKeys.ServerRedeemCodeNotFound),
                RedeemStatus.AlreadyUsed => Models.Response.Response.Fail(ReturnCode.RedeemCodeAlreadyUsed, "Redeem code has already been used", ServerKeys.ServerRedeemCodeAlreadyUsed),
                RedeemStatus.Expired => Models.Response.Response.Fail(ReturnCode.RedeemCodeExpired, "Redeem code has expired", ServerKeys.ServerRedeemCodeExpired),
                RedeemStatus.NotEnough => Models.Response.Response.Fail(ReturnCode.RedeemCodeNotEnough, "Redeem code has no remaining uses", ServerKeys.ServerRedeemCodeNotEnough),
                RedeemStatus.NoSuchUser => Models.Response.Response.Fail(ReturnCode.RedeemCodeNoSuchUser, "User does not exist", ServerKeys.ServerRedeemNoSuchUser),
                RedeemStatus.DbError => Models.Response.Response.Fail(ReturnCode.RedeemCodeDbException, "Database error", ServerKeys.ServerDatabaseError),
                _ => throw new NotSupportedException(),
            };
        }
    }
}