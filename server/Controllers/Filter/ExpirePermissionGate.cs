using Microsoft.EntityFrameworkCore;

namespace server.Controllers.Filter;

/// <summary>
/// CDN 与抽卡日志上传共用的到期门禁：登录态 → IsLicensedDeveloper 或到期时间未过。
/// 两个 <see cref="IAsyncActionFilter"/> 只有失败信封和到期字段不同——过去各抄一份 CanUse，收口到这里。
/// </summary>
internal static class ExpirePermissionGate
{
    /// <summary>放行返回 null；拦截返回应写回的失败信封。</summary>
    public static async Task<IActionResult?> EvaluateAsync(
        ActionExecutingContext context,
        AppDbContext appDbContext,
        TimeProvider timeProvider,
        Func<HutaoUser, long> expireAt,
        IActionResult insufficientEnvelope)
    {
        if (!context.TryGetUserId(out int userId))
        {
            return Fail("Please log in or register a Snap Hutao account first", ReturnCode.LoginFail, ServerKeys.ServerPassportLoginRequired);
        }

        HutaoUser? user = await appDbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == userId)
            .ConfigureAwait(false);

        if (user is null || !(user.IsLicensedDeveloper || expireAt(user) > timeProvider.GetUnixTimeSeconds()))
        {
            return insufficientEnvelope;
        }

        return null;
    }
}
