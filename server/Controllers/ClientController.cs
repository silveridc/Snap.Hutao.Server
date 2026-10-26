using server.Services.Yae;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ClientController(YaeMetadataService yaeMetadataService) : ControllerBase
{
    private const string _achievementFieldIdPrefix = "AchievementFieldId_";

    /// <summary>
    /// 成就注入配置。注入用的 YaeAchievementLib.dll 本身不含任何版本数据，RVA 全部来自本接口。
    /// </summary>
    /// <param name="feature">形如 <c>AchievementFieldId_7.1.0.json</c> 的完整文件名。</param>
    /// <param name="token">请求取消令牌。</param>
    /// <returns>已同步过的版本返回裸 JSON；未同步或名称不认识返回 404。</returns>
    [HttpGet("{feature}")]
    public async Task<IActionResult> FeatureAsync(string feature, CancellationToken token)
    {
        if (!feature.StartsWith(_achievementFieldIdPrefix, StringComparison.Ordinal) || !feature.EndsWith(".json", StringComparison.Ordinal))
        {
            return NotFound();
        }

        string version = feature[_achievementFieldIdPrefix.Length..^".json".Length];
        string? payload = await yaeMetadataService.GetPayloadAsync(version, token).ConfigureAwait(false);
        if (payload is null)
        {
            return NotFound();
        }

        // 不发 Cache-Control：游戏热更后 exe 指纹会变，同一份 URL 背后的正确内容也会变，
        // CDN 缓存住旧 RVA 组会直接把注入打崩。客户端自己已有 6 小时的内存缓存。
        return Content(payload, "application/json; charset=utf-8");
    }
}
