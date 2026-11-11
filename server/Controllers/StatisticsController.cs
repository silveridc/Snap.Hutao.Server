using server.Models.Entity.SpiralAbyss;
using server.Models.Legacy;

namespace server.Controllers;

[ApiController]
[Route("api/statistics")]
[ApiExplorerSettings(GroupName = "SpiralAbyss")]
public class StatisticsController(SpiralAbyssStatisticsService spiralAbyssStatisticsService) : ControllerBase
{
    /// <summary>
    /// Gets the Spiral Abyss statistics overview.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The Spiral Abyss overview data.</returns>
    [HttpGet("overview")]
    public IActionResult Overview([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<Overview>(LegacyStatistics.Overview, last);
    }

    /// <summary>
    /// Gets the avatar appearance rate statistics.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The avatar appearance rate data.</returns>
    [HttpGet("avatars/attendance-rate")]
    public IActionResult AttendanceRate([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarAppearanceRank>>(LegacyStatistics.AvatarAppearanceRank, last);
    }

    /// <summary>
    /// Gets the avatar usage rate statistics.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The avatar usage rate data.</returns>
    [HttpGet("avatars/utilization-rate")]
    public IActionResult UtilizationRate([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarUsageRank>>(LegacyStatistics.AvatarUsageRank, last);
    }

    /// <summary>
    /// Gets the avatar ownership rate statistics.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The avatar ownership rate data.</returns>
    [HttpGet("avatars/holding-rate")]
    public IActionResult HoldingRate([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarConstellationInfo>>(LegacyStatistics.AvatarConstellationInfo, last);
    }

    /// <summary>
    /// Gets the avatar collocation statistics.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The avatar collocation data.</returns>
    [HttpGet("avatars/collocations")]
    public IActionResult AvatarCollocation([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarCollocation>>(LegacyStatistics.AvatarCollocation, last);
    }

    /// <summary>
    /// Gets the weapon collocation statistics.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The weapon collocation data.</returns>
    [HttpGet("weapons/collocations")]
    public IActionResult WeaponCollocation([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<WeaponCollocation>>(LegacyStatistics.WeaponCollocation, last);
    }

    /// <summary>
    /// Gets the team combination statistics.
    /// </summary>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The team combination data.</returns>
    [HttpGet("teams/combinations")]
    public IActionResult Combination([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<TeamAppearance>>(LegacyStatistics.TeamAppearance, last);
    }

    /// <summary>
    /// Gets the Spiral Abyss statistics of the given type for the specified schedule.
    /// </summary>
    /// <typeparam name="T">The statistics data type.</typeparam>
    /// <param name="name">The statistics data name.</param>
    /// <param name="last">Whether to get the previous schedule.</param>
    /// <returns>The statistics data.</returns>
    private IActionResult GetStatistics<T>(string name, bool last)
        where T : class
    {
        int scheduleId = SpiralAbyssScheduleId.GetForNow();
        if (last)
        {
            scheduleId--;
        }

        T? data = spiralAbyssStatisticsService.GetStatistics<T>(name, scheduleId);
        if (data is null && typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
        {
            data = Activator.CreateInstance<T>()!;
        }

        return Success<T?>("Spiral Abyss statistics acquired successfully", data, ServerKeys.ServerStatisticsAbyssSucceed);
    }
}
