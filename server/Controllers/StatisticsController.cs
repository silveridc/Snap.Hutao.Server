using server.Models.Entity.SpiralAbyss;
using server.Models.Legacy;

namespace server.Controllers;

[ApiController]
[Route("api/statistics")]
[ApiExplorerSettings(GroupName = "SpiralAbyss")]
public class StatisticsController(SpiralAbyssStatisticsService spiralAbyssStatisticsService) : ControllerBase
{
    /// <summary>
    /// 获取深渊统计总览。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>深渊统计总览数据。</returns>
    [HttpGet("overview")]
    public IActionResult Overview([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<Overview>(LegacyStatistics.Overview, last);
    }

    /// <summary>
    /// 获取角色登场率统计。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>角色登场率数据。</returns>
    [HttpGet("avatars/attendance-rate")]
    public IActionResult AttendanceRate([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarAppearanceRank>>(LegacyStatistics.AvatarAppearanceRank, last);
    }

    /// <summary>
    /// 获取角色使用率统计。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>角色使用率数据。</returns>
    [HttpGet("avatars/utilization-rate")]
    public IActionResult UtilizationRate([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarUsageRank>>(LegacyStatistics.AvatarUsageRank, last);
    }

    /// <summary>
    /// 获取角色持有率统计。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>角色持有率数据。</returns>
    [HttpGet("avatars/holding-rate")]
    public IActionResult HoldingRate([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarConstellationInfo>>(LegacyStatistics.AvatarConstellationInfo, last);
    }

    /// <summary>
    /// 获取角色搭配统计。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>角色搭配数据。</returns>
    [HttpGet("avatars/collocations")]
    public IActionResult AvatarCollocation([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<AvatarCollocation>>(LegacyStatistics.AvatarCollocation, last);
    }

    /// <summary>
    /// 获取武器搭配统计。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>武器搭配数据。</returns>
    [HttpGet("weapons/collocations")]
    public IActionResult WeaponCollocation([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<WeaponCollocation>>(LegacyStatistics.WeaponCollocation, last);
    }

    /// <summary>
    /// 获取队伍组合统计。
    /// </summary>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>队伍组合数据。</returns>
    [HttpGet("teams/combinations")]
    public IActionResult Combination([FromQuery(Name = "Last")] bool last = false)
    {
        return GetStatistics<List<TeamAppearance>>(LegacyStatistics.TeamAppearance, last);
    }

    /// <summary>
    /// 根据名称获取指定期数的深渊统计数据。
    /// </summary>
    /// <typeparam name="T">统计数据类型。</typeparam>
    /// <param name="name">统计数据名称。</param>
    /// <param name="last">是否获取上一期数据。</param>
    /// <returns>统计数据。</returns>
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
