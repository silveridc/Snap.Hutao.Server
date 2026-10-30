using server.Models.Entity.Announcement;

namespace server.Controllers;

[ApiController]
[Route("api/announcements")]
[ApiExplorerSettings(GroupName = "Passport")]
public sealed class AnnouncementController(AnnouncementService announcementService) : ControllerBase
{
    /// <summary>
    /// 获取公告列表.
    /// </summary>
    /// <param name="exclude">需要排除的公告 ID，逗号分隔.</param>
    /// <returns>公告列表.</returns>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? exclude)
    {
        // Do not return any announcement for non hutao client requests
        if (!this.TryGetClientVersion(out Version? currentVersion))
        {
            return Success<List<EntityAnnouncement>>("Announcements acquired successfully", [], ServerKeys.ServerAnnouncementGetSucceed);
        }

        HashSet<long> excludedIds = ParseExcludedIds(exclude);
        List<EntityAnnouncement> results = await announcementService
            .GetAnnouncementsAsync(currentVersion, "ALL", excludedIds)
            .ConfigureAwait(false);

        return Success<List<EntityAnnouncement>>("Announcements acquired successfully", results, ServerKeys.ServerAnnouncementGetSucceed);
    }

    private static HashSet<long> ParseExcludedIds(string? exclude)
    {
        return (exclude ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => long.TryParse(id, out long value) ? value : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }
}
