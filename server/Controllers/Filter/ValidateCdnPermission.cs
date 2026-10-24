namespace server.Controllers.Filter;

public sealed class ValidateCdnPermission(AppDbContext appDbContext, TimeProvider timeProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        IActionResult? rejection = await ExpirePermissionGate.EvaluateAsync(
            context,
            appDbContext,
            timeProvider,
            user => user.CdnExpireAt,
            Fail("Hutao Cloud CDN service is not active or has expired", ReturnCode.GachaLogServiceNotAllowed, ServerKeys.ServerCdnServiceInsufficientTime)).ConfigureAwait(false);

        if (rejection is not null)
        {
            context.Result = rejection;
            return;
        }

        // Execute next filter.
        ActionExecutedContext result = await next().ConfigureAwait(false);
    }
}
