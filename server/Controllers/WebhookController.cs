using server.Models.Afdian;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public class WebhookController(AfdianWebhookService afdianWebhookService) : ControllerBase
{
    /// <summary>
    /// Processes an Afdian order webhook callback.
    /// </summary>
    /// <param name="request">The Afdian order webhook data.</param>
    /// <returns>The processing result.</returns>
    // [HttpGet("Incoming/Afdian")]
    // [HttpPost("Incoming/Afdian")]
    public async Task<IActionResult> IncomingAfdianAsync([FromBody] AfdianResponse<OrderWrapper> request)
    {
        await afdianWebhookService.ProcessIncomingOrderAsync(request.Data.Order).ConfigureAwait(false);
        return new JsonResult(new AfdianResponse() { ErrorCode = 200, ErrorMessage = string.Empty });
    }
}
