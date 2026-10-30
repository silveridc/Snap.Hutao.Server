namespace server.Extension;

internal static class UserAuthorizationExtension
{
    /// <summary>
    /// 从控制器上下文中获取当前用户 ID.
    /// </summary>
    /// <param name="controller">当前控制器.</param>
    /// <returns>用户 ID.</returns>
    public static int GetUserId(this ControllerBase controller)
    {
        return controller.HttpContext.GetUserId();
    }

    /// <summary>
    /// 从控制器上下文中获取当前用户 ID，调用者不是主站用户时返回 0.
    /// 后台专属账号（SSO）的令牌没有用户身份，写操作落库的「操作人」字段用 0 表示这类调用者.
    /// </summary>
    /// <param name="controller">当前控制器.</param>
    /// <returns>用户 ID，或 0.</returns>
    public static int GetUserIdOrZero(this ControllerBase controller)
    {
        return controller.HttpContext.TryGetUserId(out int userId) ? userId : 0;
    }

    /// <summary>
    /// 从操作执行上下文中获取当前用户 ID.
    /// </summary>
    /// <param name="context">操作执行上下文.</param>
    /// <returns>用户 ID.</returns>
    public static int GetUserId(this ActionExecutingContext context)
    {
        return context.HttpContext.GetUserId();
    }

    /// <summary>
    /// 从 HTTP 上下文中获取当前用户 ID.
    /// </summary>
    /// <param name="context">HTTP 上下文.</param>
    /// <returns>用户 ID.</returns>
    public static int GetUserId(this HttpContext context)
    {
        string value = context.User.Claims.Single(c => c.Type == PassportClaimTypes.UserId).Value;
        return int.Parse(value);
    }

    /// <summary>
    /// 尝试从控制器上下文中获取当前用户 ID.
    /// </summary>
    /// <param name="controller">当前控制器.</param>
    /// <param name="userId">用户 ID.</param>
    /// <returns>是否获取成功.</returns>
    public static bool TryGetUserId(this ControllerBase controller, out int userId)
    {
        return controller.HttpContext.TryGetUserId(out userId);
    }

    /// <summary>
    /// 尝试从操作执行上下文中获取当前用户 ID.
    /// </summary>
    /// <param name="context">操作执行上下文.</param>
    /// <param name="userId">用户 ID.</param>
    /// <returns>是否获取成功.</returns>
    public static bool TryGetUserId(this ActionExecutingContext context, out int userId)
    {
        return context.HttpContext.TryGetUserId(out userId);
    }

    /// <summary>
    /// 尝试从 HTTP 上下文中获取当前用户 ID.
    /// </summary>
    /// <param name="context">HTTP 上下文.</param>
    /// <param name="userId">用户 ID.</param>
    /// <returns>是否获取成功.</returns>
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
    /// 获取当前登录用户实体.
    /// </summary>
    /// <param name="controller">当前控制器.</param>
    /// <param name="users">用户数据集合.</param>
    /// <returns>用户实体；未登录时为 null.</returns>
    public static async ValueTask<HutaoUser?> GetUserAsync(this ControllerBase controller, DbSet<HutaoUser> users)
    {
        if (controller.TryGetUserId(out int userId))
        {
            return await users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId).ConfigureAwait(false);
        }

        return default;
    }

    /// <summary>
    /// 尝试从控制器上下文中获取当前后台专属账号 ID.
    /// </summary>
    /// <param name="controller">当前控制器.</param>
    /// <param name="accountId">后台账号 ID.</param>
    /// <returns>是否获取成功.</returns>
    public static bool TryGetAdminAccountId(this ControllerBase controller, out int accountId)
    {
        return controller.HttpContext.TryGetAdminAccountId(out accountId);
    }

    /// <summary>
    /// 尝试从操作执行上下文中获取当前后台专属账号 ID.
    /// </summary>
    /// <param name="context">操作执行上下文.</param>
    /// <param name="accountId">后台账号 ID.</param>
    /// <returns>是否获取成功.</returns>
    public static bool TryGetAdminAccountId(this ActionExecutingContext context, out int accountId)
    {
        return context.HttpContext.TryGetAdminAccountId(out accountId);
    }

    /// <summary>
    /// 尝试从 HTTP 上下文中获取当前后台专属账号 ID.
    /// 后台令牌只携带该声明、不携带用户身份，两种令牌因此不会互相冒充.
    /// </summary>
    /// <param name="context">HTTP 上下文.</param>
    /// <param name="accountId">后台账号 ID.</param>
    /// <returns>是否获取成功.</returns>
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
