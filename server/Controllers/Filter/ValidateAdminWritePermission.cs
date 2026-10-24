using server.Services.Admin;

namespace server.Controllers.Filter;

/// <summary>
/// 写操作门禁：启用单点登录后，<c>/Service/*</c> 的非 GET 请求只放行有权限的身份——
/// 后台专属账号本身，或绑定了可用后台账号的主站账号（启动器登录用的就是后者，
/// 因为启动器只会携带主站令牌）。主站维护者没被绑定时就降级为只读：原来的账号照样能登录、能看，只是不能再改。
/// <para>
/// 未启用单点登录时本过滤器整体放行，保持「配置没配好之前不会把自己关在门外」的部署语义。
/// </para>
/// </summary>
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
