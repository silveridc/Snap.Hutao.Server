using server.Extension;
using server.Services.Admin;

namespace server.Controllers.Filter;

public sealed class ValidateMaintainPermission(AppDbContext appDbContext, IDistributedCache cache, AdminSsoService adminSsoService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.TryGetAdminAccountId(out int accountId))
        {
            if (await adminSsoService.IsAccountUsableAsync(accountId, context.HttpContext.RequestAborted).ConfigureAwait(false))
            {
                await next().ConfigureAwait(false);
                return;
            }
        }

        if (context.TryGetUserId(out int userId))
        {
            if (await IsMaintainerAsync(userId).ConfigureAwait(false) || await adminSsoService.IsUserProvisionedAsync(userId, context.HttpContext.RequestAborted).ConfigureAwait(false))
            {
                await next().ConfigureAwait(false);
                return;
            }
        }

        context.Result = Fail("Only official staff can perform this action", ReturnCode.ServiceKeyInvalid, ServerKeys.ServerMaintainerOnly);
    }

    private async Task<bool> IsMaintainerAsync(int userId)
    {
        string cacheKey = $"User:IsMaintainer:{userId}";

        return await cache.GetOrCreateAsync(cacheKey, TimeSpan.FromMinutes(15), async () =>
            await appDbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.IsMaintainer)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false)).ConfigureAwait(false);
    }
}
