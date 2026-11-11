namespace server.Filter;

internal static class ExpirePermissionGate
{
    /// <summary>
    /// Returns <see langword="null"/> to allow the request; otherwise, returns the rejection envelope to write back.
    /// </summary>
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
