namespace server.Filter;

public sealed class ValidateGachaLogPermission(AppDbContext appDbContext, TimeProvider timeProvider) : IAsyncActionFilter
{
    /// <summary>
    /// Allows the request only while the gacha log service of the account is active.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="next">The next action delegate.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
