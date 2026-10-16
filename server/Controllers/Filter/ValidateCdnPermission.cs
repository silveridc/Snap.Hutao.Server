namespace server.Controllers.Filter
{
    public sealed class ValidateCdnPermission(AppDbContext appDbContext) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.TryGetUserId(out int userId))
            {
                context.Result = Response.Fail(ReturnCode.LoginFail, "Please log in or register a Snap Hutao account first", ServerKeys.ServerPassportLoginRequired);
                return;
            }

            if (!await CanUseCdnAsync(userId, user => user.CdnExpireAt).ConfigureAwait(false))
            {
                context.Result = Response.Fail(ReturnCode.GachaLogServiceNotAllowed, "Hutao Cloud CDN service is not active or has expired", ServerKeys.ServerCdnServiceInsufficientTime);
                return;
            }

            // Execute next filter.
            ActionExecutedContext result = await next().ConfigureAwait(false);
        }

        // Use selector for compatibility.
        private async Task<bool> CanUseCdnAsync(int userId, Func<HutaoUser, long> expireTarget)
        {
            HutaoUser? user = await appDbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId)
                .ConfigureAwait(false);

            if (user is null)
            {
                return false;
            }

            return user.IsLicensedDeveloper || expireTarget(user) > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}