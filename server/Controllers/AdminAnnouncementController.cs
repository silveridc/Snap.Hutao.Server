using server.Filter;
using server.Models.Admin;
using server.Models.Entity.Announcement;
using server.Models.Upload;

namespace server.Controllers;

[Authorize]
[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(ValidateMaintainPermission), Order = 1)]
[ServiceFilter(typeof(ValidateAdminWritePermission), Order = 2)]
[ServiceFilter(typeof(AuditLogActionFilter), Order = 3)]
[ApiExplorerSettings(GroupName = "Services")]
public sealed class AdminAnnouncementController(AnnouncementService announcementService) : ControllerBase
{
    /// <summary>
    /// 上传公告。
    /// </summary>
    /// <param name="announcement">公告内容。</param>
    /// <returns>操作结果。</returns>
    [HttpPost("announcements")]
    public async Task<IActionResult> UploadAnnouncementAsync([FromBody] HutaoUploadAnnouncement announcement)
    {
        await announcementService.ProcessUploadAnnouncementAsync(announcement).ConfigureAwait(false);
        return Success("Announcement uploaded successfully", ServerKeys.ServerAnnouncementUploadSucceed);
    }

    /// <summary>
    /// 分页获取公告列表（含历史，不做版本过滤）。
    /// </summary>
    [HttpGet("announcements")]
    public async Task<IActionResult> AnnouncementListAsync([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        PagedResult<EntityAnnouncement> result = await announcementService.GetAllAsync(page, pageSize).ConfigureAwait(false);
        return Success<PagedResult<EntityAnnouncement>>("Announcement list acquired successfully", result, ServerKeys.ServerAdminAnnouncementListSucceed);
    }

    /// <summary>
    /// 删除指定公告。
    /// </summary>
    [HttpDelete("announcements/{id}")]
    public async Task<IActionResult> AnnouncementDeleteAsync([FromRoute] long id, [FromQuery] string locale = "ALL")
    {
        bool removed = await announcementService.DeleteAsync(id, locale).ConfigureAwait(false);
        return removed
            ? Success("Announcement deleted successfully", ServerKeys.ServerAdminAnnouncementDeleteSucceed)
            : Fail("Announcement does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminAnnouncementNotFound);
    }

    /// <summary>
    /// 编辑公告。
    /// </summary>
    [HttpPut("announcements/{id}")]
    public async Task<IActionResult> AnnouncementUpdateAsync([FromRoute] long id, [FromBody] AnnouncementUpdateRequest request)
    {
        request.Id = id;

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return Fail("Title and content must not be empty", ReturnCode.InvalidUploadData, ServerKeys.ServerAdminAnnouncementContentRequired);
        }

        bool ok = await announcementService.UpdateAsync(request).ConfigureAwait(false);
        return ok
            ? Success("Announcement updated successfully", ServerKeys.ServerAdminAnnouncementUpdateSucceed)
            : Fail("Announcement does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminAnnouncementNotFound);
    }

    /// <summary>
    /// 上/下架公告。
    /// </summary>
    [HttpPatch("announcements/{id}")]
    public async Task<IActionResult> AnnouncementSetEnabledAsync([FromRoute] long id, [FromQuery] bool enabled, [FromQuery] string locale = "ALL")
    {
        bool ok = await announcementService.SetEnabledAsync(id, locale, enabled).ConfigureAwait(false);
        return ok
            ? Success(enabled ? "Announcement enabled" : "Announcement disabled", enabled ? ServerKeys.ServerAdminAnnouncementEnableSucceed : ServerKeys.ServerAdminAnnouncementDisableSucceed)
            : Fail("Announcement does not exist", ReturnCode.NotFound, ServerKeys.ServerAdminAnnouncementNotFound);
    }
}
