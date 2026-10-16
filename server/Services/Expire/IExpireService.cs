namespace server.Services.Expire
{
    internal interface IExpireService
    {
        ValueTask<TermExtendResult> ExtendTermForUserNameAsync(string userName, int days);

        ValueTask<TermExtendResult> ExtendTermForUserAsync(DbSet<HutaoUser> users, HutaoUser user, int days);

        ValueTask<DateTimeOffset> ExtendTermForAllUsersAsync(int days);
    }
}