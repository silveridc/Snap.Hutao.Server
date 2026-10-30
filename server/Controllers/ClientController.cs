using server.Services.Yae;

namespace server.Controllers;

[ApiController]
[Route("api/client")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class ClientController(YaeMetadataService yaeMetadataService) : ControllerBase
{
    private const string _achievementFieldIdPrefix = "AchievementFieldId_";

    /// <summary>
    /// get yae data.
    /// </summary>
    /// <param name="feature">feat.</param>
    /// <param name="cancellationToken">cancellationToken.</param>
    /// <returns>
    /// data.
    /// </returns>
    [HttpGet("features/{feature}")]
    public async Task<IActionResult> FeatureAsync(string feature, CancellationToken cancellationToken)
    {
        if (!feature.StartsWith(_achievementFieldIdPrefix, StringComparison.Ordinal))
        {
            return NotFound();
        }

        string version = feature[_achievementFieldIdPrefix.Length..];
        string? payload = await yaeMetadataService.GetPayloadAsync(version, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            return NotFound();
        }

        return Content(payload, "application/json; charset=utf-8");
    }
}
