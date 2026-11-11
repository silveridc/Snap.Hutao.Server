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
    /// Gets the currently active Snap Hutao client release.
    /// </summary>
    [HttpGet("releases/current")]
    public async Task<IActionResult> ReleaseCurrentAsync()
    {
        HutaoReleaseItem? release = await releaseService.GetCurrentAsync().ConfigureAwait(false);
        return Success<HutaoReleaseItem?>("Current release retrieved successfully", release, ServerKeys.ServerAdminReleaseCurrentSucceed);
    }

    /// <summary>
    /// Gets a page of Snap Hutao client release history.
    /// </summary>
    /// <param name="page">The one-based page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The requested page of releases.</returns>
    [HttpGet("releases")]
    public async Task<IActionResult> ReleaseListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<HutaoReleaseItem> result = await releaseService.GetListAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<HutaoReleaseItem>>("Release history retrieved successfully", result, ServerKeys.ServerAdminReleaseListSucceed);
    }

    /// <summary>
    /// Publishes and activates a new Snap Hutao client release.
    /// </summary>
    /// <param name="request">The publish request.</param>
    /// <returns>The created release.</returns>
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
    /// Activates a historical release, used for explicit rollbacks.
    /// </summary>
    /// <param name="id">The release identifier.</param>
    /// <param name="request">The activation request.</param>
    /// <returns>The operation result.</returns>
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
    /// Deactivates the currently active release so clients no longer receive its update.
    /// </summary>
    /// <param name="id">The release identifier.</param>
    /// <returns>The operation result.</returns>
    [HttpDelete("releases/{id}/activation")]
    public async Task<IActionResult> ReleaseDeactivateAsync([FromRoute] long id)
    {
        bool deactivated = await releaseService.DeactivateAsync(id).ConfigureAwait(false);
        return deactivated
            ? Success("Release deactivated", ServerKeys.ServerAdminReleaseDeactivateSucceed)
            : Fail("Release is not active", ReturnCode.NotFound, ServerKeys.ServerAdminReleaseNotFound);
    }

    /// <summary>
    /// Lists the objects in the patch storage bucket so the admin UI can pick one to publish.
    /// </summary>
    /// <param name="prefix">The optional key prefix to filter by.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The bucket object listing.</returns>
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
    /// Publishes a release from the patch storage bucket: the server reads the object, computes its SHA-256,
    /// and builds the download mirrors from it.
    /// </summary>
    /// <param name="request">The publish request referencing the bucket object.</param>
    /// <param name="cancellationToken">The request cancellation token.</param>
    /// <returns>The created release.</returns>
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
