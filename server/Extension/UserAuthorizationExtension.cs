namespace server.Extension;

internal static class UserAuthorizationExtension
{
    /// <summary>
    /// Gets the current user ID from the controller context.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <returns>The user ID.</returns>
    public static int GetUserId(this ControllerBase controller)
    {
        return controller.HttpContext.GetUserId();
    }

    /// <summary>
    /// Gets the current user ID from the controller context, returning 0 when the caller is not a Hutao site user.
    /// Admin SSO tokens carry no user identity, so audit rows store 0 as the operator for such callers.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <returns>The user ID, or 0.</returns>
    public static int GetUserIdOrZero(this ControllerBase controller)
    {
        return controller.HttpContext.TryGetUserId(out int userId) ? userId : 0;
    }

    /// <summary>
    /// Gets the current user ID from the action executing context.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <returns>The user ID.</returns>
    public static int GetUserId(this ActionExecutingContext context)
    {
        return context.HttpContext.GetUserId();
    }

    /// <summary>
    /// Gets the current user ID from the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The user ID.</returns>
    public static int GetUserId(this HttpContext context)
    {
        string value = context.User.Claims.Single(c => c.Type == PassportClaimTypes.UserId).Value;
        return int.Parse(value);
    }

    /// <summary>
    /// Tries to get the current user ID from the controller context.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns><see langword="true"/> if the user ID was acquired; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetUserId(this ControllerBase controller, out int userId)
    {
        return controller.HttpContext.TryGetUserId(out userId);
    }

    /// <summary>
    /// Tries to get the current user ID from the action executing context.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns><see langword="true"/> if the user ID was acquired; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetUserId(this ActionExecutingContext context, out int userId)
    {
        return context.HttpContext.TryGetUserId(out userId);
    }

    /// <summary>
    /// Tries to get the current user ID from the HTTP context.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="userId">The user ID.</param>
    /// <returns><see langword="true"/> if the user ID was acquired; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetUserId(this HttpContext context, out int userId)
    {
        string? value = context.User.Claims.SingleOrDefault(c => c.Type == PassportClaimTypes.UserId)?.Value;

        if (string.IsNullOrEmpty(value))
        {
            userId = 0;
            return false;
        }

        return int.TryParse(value, out userId);
    }

    /// <summary>
    /// Gets the currently signed-in user entity.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <param name="users">The user data set.</param>
    /// <returns>The user entity, or <see langword="null"/> when not signed in.</returns>
    public static async ValueTask<HutaoUser?> GetUserAsync(this ControllerBase controller, DbSet<HutaoUser> users)
    {
        if (controller.TryGetUserId(out int userId))
        {
            return await users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId).ConfigureAwait(false);
        }

        return default;
    }

    /// <summary>
    /// Tries to get the current admin account ID from the controller context.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <param name="accountId">The admin account ID.</param>
    /// <returns><see langword="true"/> if the account ID was acquired; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetAdminAccountId(this ControllerBase controller, out int accountId)
    {
        return controller.HttpContext.TryGetAdminAccountId(out accountId);
    }

    /// <summary>
    /// Tries to get the current admin account ID from the action executing context.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="accountId">The admin account ID.</param>
    /// <returns><see langword="true"/> if the account ID was acquired; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetAdminAccountId(this ActionExecutingContext context, out int accountId)
    {
        return context.HttpContext.TryGetAdminAccountId(out accountId);
    }

    /// <summary>
    /// Tries to get the current admin account ID from the HTTP context.
    /// Admin tokens carry only this claim and no user identity, so the two token kinds cannot impersonate each other.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="accountId">The admin account ID.</param>
    /// <returns><see langword="true"/> if the account ID was acquired; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetAdminAccountId(this HttpContext context, out int accountId)
    {
        string? value = context.User.Claims.SingleOrDefault(c => c.Type == AdminClaimTypes.AccountId)?.Value;

        if (string.IsNullOrEmpty(value))
        {
            accountId = 0;
            return false;
        }

        return int.TryParse(value, out accountId);
    }
}
