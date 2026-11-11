using server.Models.Entity.Announcement;

namespace server.Controllers;

[ApiController]
[Route("api/announcements")]
[ApiExplorerSettings(GroupName = "Passport")]
public sealed class AnnouncementController(AnnouncementService announcementService) : ControllerBase
{
    /// <summary>
    /// Gets the announcement list.
    /// </summary>
    /// <param name="exclude">The announcement IDs to exclude, separated by commas.</param>
    /// <returns>The announcement list.</returns>
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

    /// <summary>
    /// Parses the comma-separated excluded announcement IDs.
    /// </summary>
    /// <param name="exclude">The raw excluded ID list.</param>
    /// <returns>The set of valid excluded IDs.</returns>
    private static HashSet<long> ParseExcludedIds(string? exclude)
    {
        return (exclude ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => long.TryParse(id, out long value) ? value : 0)
            .Where(id => id > 0)
            .ToHashSet();
    }
}
