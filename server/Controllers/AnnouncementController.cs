using server.Models.Entity.Announcement;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public sealed class AnnouncementController(AnnouncementService announcementService) : ControllerBase
    {
        /// <summary>
        /// 获取公告列表。
        /// </summary>
        /// <param name="excludedIds">需要排除的公告 ID 集合。</param>
        /// <returns>公告列表。</returns>
        [HttpPost("List")]
        public async Task<IActionResult> List([FromBody] HashSet<long> excludedIds)
        {
            // Do not return any announcement for non hutao client requests
            if (!this.TryGetClientVersion(out Version? currentVersion))
            {
                return Models.Response.Response<List<EntityAnnouncement>>.Success("Announcements acquired successfully", ServerKeys.ServerAnnouncementGetSucceed, []);
            }

            List<EntityAnnouncement> results = await announcementService
                .GetAnnouncementsAsync(currentVersion, "ALL", excludedIds)
                .ConfigureAwait(false);

            return Models.Response.Response<List<EntityAnnouncement>>.Success("Announcements acquired successfully", ServerKeys.ServerAnnouncementGetSucceed, results);
        }
    }
}