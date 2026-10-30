using server.Filter;
using server.Models.Admin;
using server.Models.Redeem;
using server.Services.Admin;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminRedeemCodeController(
    RedeemService redeemService,
    AdminQueryService adminQueryService,
    AppDbContext appDbContext) : ControllerBase
{
    /// <summary>
    /// 生成兑换码.
    /// </summary>
    /// <param name="req">兑换码生成请求.</param>
    /// <returns>生成结果.</returns>
    [HttpPost("redeem-codes/generation")]
    public async Task<IActionResult> GenerateRedeemCodesAsync([FromBody] RedeemGenerateRequest req)
    {
        HutaoUser? hutaoUser = await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(hutaoUser?.UserName);
        req.Creator = hutaoUser.UserName;

        RedeemGenerateResponse response = await redeemService.GenerateRedeemCodesAsync(req).ConfigureAwait(false);
        return Success<RedeemGenerateResponse>("Redeem codes generated successfully", response, ServerKeys.ServerAdminRedeemGenerateSucceed);
    }

    /// <summary>
    /// 分页获取兑换码列表.
    /// </summary>
    [HttpGet("redeem-codes")]
    public async Task<IActionResult> RedeemListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<RedeemCodeItem> result = await adminQueryService.GetRedeemCodesAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success<PagedResult<RedeemCodeItem>>("Redeem code list acquired successfully", result, ServerKeys.ServerAdminRedeemListSucceed);
    }

    /// <summary>
    /// 某张兑换码的使用者清单.
    /// </summary>
    /// <param name="id">兑换码主键.</param>
    /// <returns>使用该兑换码的用户与时间.</returns>
    [HttpGet("redeem-codes/{id}/usages")]
    public async Task<IActionResult> RedeemUsesAsync([FromRoute] uint id)
    {
        List<RedeemCodeUsageItem> items = await adminQueryService.GetRedeemCodeUsesAsync(id).ConfigureAwait(false);
        return Success<List<RedeemCodeUsageItem>>("Redeem code usages acquired successfully", items, ServerKeys.ServerAdminRedeemUsesSucceed);
    }

    /// <summary>
    /// 编辑兑换码（服务、时长、备注、过期时间、可用次数）.
    /// </summary>
    [HttpPut("redeem-codes/{id}")]
    public async Task<IActionResult> RedeemUpdateAsync([FromRoute] uint id, [FromBody] RedeemCodeUpdateRequest request)
    {
        request.Id = id;

        bool ok = await adminQueryService.UpdateRedeemCodeAsync(request).ConfigureAwait(false);
        return ok
            ? Success("Redeem code updated successfully", ServerKeys.ServerAdminRedeemUpdateSucceed)
            : Fail("Redeem code does not exist", ReturnCode.NotFound, ServerKeys.ServerRedeemCodeNotFound);
    }

    /// <summary>
    /// 删除兑换码（同时删除其使用记录）.
    /// </summary>
    [HttpDelete("redeem-codes/{id}")]
    public async Task<IActionResult> RedeemDeleteAsync([FromRoute] uint id)
    {
        bool removed = await adminQueryService.DeleteRedeemCodeAsync(id).ConfigureAwait(false);
        return removed
            ? Success("Redeem code deleted successfully", ServerKeys.ServerAdminRedeemDeleteSucceed)
            : Fail("Redeem code does not exist", ReturnCode.NotFound, ServerKeys.ServerRedeemCodeNotFound);
    }

    /// <summary>
    /// 批量删除兑换码.
    /// </summary>
    [HttpPost("redeem-codes/batch-deletion")]
    public async Task<IActionResult> RedeemBatchDeleteAsync([FromBody] BatchIdRequest request)
    {
        long removed = await adminQueryService.BatchDeleteRedeemCodesAsync(request.Ids).ConfigureAwait(false);
        return Success<long>($"Deleted {removed} redeem codes", removed);
    }
}
