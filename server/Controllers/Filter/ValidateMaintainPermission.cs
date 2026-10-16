using server.Extension;

namespace server.Controllers.Filter;

public sealed class ValidateMaintainPermission(AppDbContext appDbContext, IDistributedCache cache) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.TryGetUserId(out int userId))
        {
            if (await IsMaintainerAsync(userId).ConfigureAwait(false))
            {
                await next().ConfigureAwait(false);
                return;
            }
        }

        context.Result = Response.Fail(ReturnCode.ServiceKeyInvalid, "Only official staff can perform this action", ServerKeys.ServerMaintainerOnly);
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