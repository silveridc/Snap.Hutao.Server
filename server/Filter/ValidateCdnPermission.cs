namespace server.Filter;

public sealed class ValidateCdnPermission(AppDbContext appDbContext, TimeProvider timeProvider) : IAsyncActionFilter
{
    /// <summary>
    /// Allows the request only while the CDN service of the account is active.
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
