using server.Services.Admin;

namespace server.Filter;

public sealed class ValidateAdminWritePermission(AdminSsoService adminSsoService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!adminSsoService.IsEnabled || HttpMethods.IsGet(context.HttpContext.Request.Method))
        {
            await next().ConfigureAwait(false);
            return;
        }

        if (context.TryGetAdminAccountId(out int accountId) && await adminSsoService.IsAccountUsableAsync(accountId, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            await next().ConfigureAwait(false);
            return;
        }

        if (context.TryGetUserId(out int userId) && await adminSsoService.IsUserProvisionedAsync(userId, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            await next().ConfigureAwait(false);
            return;
        }

        context.Result = Fail("Only SSO admin accounts can modify data", ReturnCode.WriteRequiresSsoAdmin, ServerKeys.ServerAdminWriteRequiresSso);
    }
}
