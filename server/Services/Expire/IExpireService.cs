namespace server.Services.Expire;

internal interface IExpireService
{
    public ValueTask<TermExtendResult> ExtendTermForUserNameAsync(string userName, int days);

    public ValueTask<TermExtendResult> ExtendTermForUserAsync(DbSet<HutaoUser> users, HutaoUser user, int days);

    public ValueTask<DateTimeOffset> ExtendTermForAllUsersAsync(int days);
}
