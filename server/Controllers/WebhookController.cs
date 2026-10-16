using server.Models.Afdian;

namespace server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class WebhookController(AfdianWebhookService afdianWebhookService) : ControllerBase
    {
        /// <summary>
        /// 处理爱发电订单回调。
        /// </summary>
        /// <param name="request">爱发电订单回调数据。</param>
        /// <returns>处理结果。</returns>
        //[HttpGet("Incoming/Afdian")]
        //[HttpPost("Incoming/Afdian")]
        public async Task<IActionResult> IncomingAfdianAsync([FromBody] AfdianResponse<OrderWrapper> request)
        {
            await afdianWebhookService.ProcessIncomingOrderAsync(request.Data.Order).ConfigureAwait(false);
            return new JsonResult(new AfdianResponse() { ErrorCode = 200, ErrorMessage = string.Empty });
        }
    }
}