namespace server.Filter;

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
