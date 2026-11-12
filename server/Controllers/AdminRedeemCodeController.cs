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
    /// Generates redeem codes.
    /// </summary>
    /// <param name="req">The redeem code generation request.</param>
    /// <returns>The generation result.</returns>
    [HttpPost("redeem-codes/generation")]
    public async Task<IActionResult> GenerateRedeemCodesAsync([FromBody] RedeemGenerateRequest req)
    {
        HutaoUser? hutaoUser = await this.GetUserAsync(appDbContext.Users).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(hutaoUser?.UserName);
        req.Creator = hutaoUser.UserName;

        RedeemGenerateResponse response = await redeemService.GenerateRedeemCodesAsync(req).ConfigureAwait(false);
        return Success("Redeem codes generated successfully", response, ServerKeys.ServerAdminRedeemGenerateSucceed);
    }

    /// <summary>
    /// Gets a page of redeem codes.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="keyword">The optional keyword matched against code, description, and creator.</param>
    /// <returns>The requested page of redeem codes.</returns>
    [HttpGet("redeem-codes")]
    public async Task<IActionResult> RedeemListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? keyword = null)
    {
        PagedResult<RedeemCodeItem> result = await adminQueryService.GetRedeemCodesAsync(page, pageSize, keyword).ConfigureAwait(false);
        return Success("Redeem code list acquired successfully", result, ServerKeys.ServerAdminRedeemListSucceed);
    }

    /// <summary>
    /// Gets the list of users who redeemed the specified redeem code.
    /// </summary>
    /// <param name="id">The redeem code identifier.</param>
    /// <returns>The users and the time they redeemed the code.</returns>
    [HttpGet("redeem-codes/{id}/usages")]
    public async Task<IActionResult> RedeemUsesAsync([FromRoute] uint id)
    {
        List<RedeemCodeUsageItem> items = await adminQueryService.GetRedeemCodeUsesAsync(id).ConfigureAwait(false);
        return Success("Redeem code usages acquired successfully", items, ServerKeys.ServerAdminRedeemUsesSucceed);
    }

    /// <summary>
    /// Updates a redeem code, including its service, duration, description, expiry, and usage limit.
    /// </summary>
    /// <param name="id">The redeem code identifier.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The operation result.</returns>
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
    /// Deletes a redeem code together with its redemption usage entries.
    /// </summary>
    /// <param name="id">The redeem code identifier.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("redeem-codes/{id}")]
    public async Task<IActionResult> RedeemDeleteAsync([FromRoute] uint id)
    {
        bool removed = await adminQueryService.DeleteRedeemCodeAsync(id).ConfigureAwait(false);
        return removed
            ? Success("Redeem code deleted successfully", ServerKeys.ServerAdminRedeemDeleteSucceed)
            : Fail("Redeem code does not exist", ReturnCode.NotFound, ServerKeys.ServerRedeemCodeNotFound);
    }

    /// <summary>
    /// Deletes multiple redeem codes in one request.
    /// </summary>
    /// <param name="request">The identifiers of the redeem codes to delete.</param>
    /// <returns>The number of deleted redeem codes.</returns>
    [HttpPost("redeem-codes/batch-deletion")]
    public async Task<IActionResult> RedeemBatchDeleteAsync([FromBody] BatchIdRequest request)
    {
        long removed = await adminQueryService.BatchDeleteRedeemCodesAsync(request.Ids).ConfigureAwait(false);
        return Success($"Deleted {removed} redeem codes", removed);
    }
}
