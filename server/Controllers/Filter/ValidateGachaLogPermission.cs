namespace server.Controllers.Filter
{
    public sealed class ValidateGachaLogPermission(AppDbContext appDbContext) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.TryGetUserId(out int userId))
            {
                context.Result = Response.Fail(ReturnCode.LoginFail, "Please log in or register a Snap Hutao account first", ServerKeys.ServerPassportLoginRequired);
                return;
            }

            if (!await CanUseGachaLogServiceAsync(userId).ConfigureAwait(false))
            {
                context.Result = Response.Fail(ReturnCode.GachaLogServiceNotAllowed, "Gacha log upload service is not active or has expired", ServerKeys.ServerGachaLogServiceInsufficientTime);
                return;
            }

            // Execute next filter.
            ActionExecutedContext result = await next().ConfigureAwait(false);
        }

        private async Task<bool> CanUseGachaLogServiceAsync(int userId)
        {
            HutaoUser? user = await appDbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Id == userId)
                .ConfigureAwait(false);

            if (user is null)
            {
                return false;
            }

            return user.IsLicensedDeveloper || user.GachaLogExpireAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}