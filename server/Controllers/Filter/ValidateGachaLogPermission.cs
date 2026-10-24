namespace server.Controllers.Filter;

public sealed class ValidateGachaLogPermission(AppDbContext appDbContext, TimeProvider timeProvider) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        IActionResult? rejection = await ExpirePermissionGate.EvaluateAsync(
            context,
            appDbContext,
            timeProvider,
            user => user.GachaLogExpireAt,
            Fail("Gacha log upload service is not active or has expired", ReturnCode.GachaLogServiceNotAllowed, ServerKeys.ServerGachaLogServiceInsufficientTime)).ConfigureAwait(false);

        if (rejection is not null)
        {
            context.Result = rejection;
            return;
        }

        // Execute next filter.
        ActionExecutedContext result = await next().ConfigureAwait(false);
    }
}
