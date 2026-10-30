using server.Filter;
using server.Models.Admin;
using server.Services.Admin;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminReleaseController(ReleaseService releaseService, ReleaseBucketService releaseBucketService) : ControllerBase
{
    /// <summary>
    /// 获取当前激活的 Snap Hutao 客户端版本.
    /// </summary>
    [HttpGet("releases/current")]
    public async Task<IActionResult> ReleaseCurrentAsync()
    {
        HutaoReleaseItem? release = await releaseService.GetCurrentAsync().ConfigureAwait(false);
        return Success<HutaoReleaseItem?>("Current release retrieved successfully", release, ServerKeys.ServerAdminReleaseCurrentSucceed);
    }

    /// <summary>
    /// 分页获取 Snap Hutao 客户端发布历史.
    /// </summary>
    [HttpGet("releases")]
    public async Task<IActionResult> ReleaseListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<HutaoReleaseItem> result = await releaseService.GetListAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<HutaoReleaseItem>>("Release history retrieved successfully", result, ServerKeys.ServerAdminReleaseListSucceed);
    }

    /// <summary>
    /// 发布并激活新的 Snap Hutao 客户端版本.
    /// </summary>
    [HttpPost("releases")]
    public async Task<IActionResult> ReleasePublishAsync([FromBody] HutaoReleasePublishRequest request)
    {
        (bool success, string message, string? localizationKey, HutaoReleaseItem? release) = await releaseService
            .PublishAsync(request, this.GetUserIdOrZero())
            .ConfigureAwait(false);

        return success
            ? Success<HutaoReleaseItem>(message, release!, localizationKey!)
            : localizationKey is null
                ? Fail(message, ReturnCode.InvalidUploadData)
                : Fail(message, ReturnCode.InvalidUploadData, localizationKey);
    }

    /// <summary>
    /// 激活历史版本，用于显式回滚.
    /// </summary>
    [HttpPut("releases/{id}/activation")]
    public async Task<IActionResult> ReleaseActivateAsync([FromRoute] long id, [FromBody] HutaoReleaseActivateRequest request)
    {
        request.Id = id;

        bool activated = await releaseService.ActivateAsync(request.Id).ConfigureAwait(false);
        return activated
            ? Success("Release activated", ServerKeys.ServerAdminReleaseActivateSucceed)
            : Fail("Release does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminReleaseNotFound);
    }

    /// <summary>
    /// 下架当前激活的版本，客户端将不再收到该版本更新.
    /// </summary>
    [HttpDelete("releases/{id}/activation")]
    public async Task<IActionResult> ReleaseDeactivateAsync([FromRoute] long id)
    {
        bool deactivated = await releaseService.DeactivateAsync(id).ConfigureAwait(false);
        return deactivated
            ? Success("Release deactivated", ServerKeys.ServerAdminReleaseDeactivateSucceed)
            : Fail("Release is not active", ReturnCode.NotFound, ServerKeys.ServerAdminReleaseNotFound);
    }

    /// <summary>
    /// 列出补丁存储桶中的对象，供后台按对象发布时选择.
    /// </summary>
    [HttpGet("releases/bucket-objects")]
    public async Task<IActionResult> ReleaseBucketObjectsAsync([FromQuery] string? prefix, CancellationToken cancellationToken)
    {
        (ReleaseBucketListResult? result, string message, string? localizationKey) = await releaseBucketService
            .ListAsync(prefix, cancellationToken)
            .ConfigureAwait(false);

        return localizationKey is null
            ? Success("Bucket objects retrieved successfully", result!, ServerKeys.ServerAdminReleaseBucketListSucceed)
            : Fail(message, ReturnCode.InvalidUploadData, localizationKey);
    }

    /// <summary>
    /// 从补丁存储桶发布版本：服务端读取对象计算 SHA-256，并据此生成下载镜像.
    /// </summary>
    [HttpPost("releases/from-bucket")]
    public async Task<IActionResult> ReleaseBucketPublishAsync([FromBody] HutaoReleasePublishFromBucketRequest request, CancellationToken cancellationToken)
    {
        (bool success, string message, string? localizationKey, HutaoReleaseItem? release) = await releaseBucketService
            .PublishAsync(request, this.GetUserIdOrZero(), cancellationToken)
            .ConfigureAwait(false);

        return success
            ? Success<HutaoReleaseItem>(message, release!, localizationKey!)
            : localizationKey is null
                ? Fail(message, ReturnCode.InvalidUploadData)
                : Fail(message, ReturnCode.InvalidUploadData, localizationKey);
    }
}
