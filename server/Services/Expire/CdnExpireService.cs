using server.Extension;
using server.Models.Context;
using server.Models.Entity.Passport;

namespace server.Services.Expire
{
    // Singleton
    public sealed class CdnExpireService(IServiceProvider serviceProvider) : IExpireService
    {
        public async ValueTask<TermExtendResult> ExtendTermForUserNameAsync(string userName, int days)
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            UserManager<HutaoUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<HutaoUser>>();

            HutaoUser? user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);

            if (user == null)
            {
                return new(TermExtendResultKind.NoSuchUser);
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (user.CdnExpireAt < now)
            {
                user.CdnExpireAt = now;
            }

            user.CdnExpireAt += (long)TimeSpan.FromDays(days).TotalSeconds;

            IdentityResult result = await userManager.UpdateAsync(user).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return new(TermExtendResultKind.DbError);
            }

            return new(TermExtendResultKind.Ok, DateTimeOffset.FromUnixTimeSeconds(user.CdnExpireAt));
        }

        public async ValueTask<TermExtendResult> ExtendTermForUserAsync(DbSet<HutaoUser> users, HutaoUser user, int days)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (user.CdnExpireAt < now)
            {
                user.CdnExpireAt = now;
            }

            user.CdnExpireAt += (long)TimeSpan.FromDays(days).TotalSeconds;

            int result = await users.UpdateAndSaveAsync(user).ConfigureAwait(false);

            if (result is 0)
            {
                return new(TermExtendResultKind.DbError);
            }

            return new(TermExtendResultKind.Ok, DateTimeOffset.FromUnixTimeSeconds(user.CdnExpireAt));
        }

        public async ValueTask<DateTimeOffset> ExtendTermForAllUsersAsync(int days)
        {
            long nowTick = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            int seconds = days * 24 * 60 * 60;

            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await appDbContext.Users
                    .Where(user => user.CdnExpireAt < nowTick)
                    .ExecuteUpdateAsync(user => user.SetProperty(u => u.CdnExpireAt, u => nowTick))
                    .ConfigureAwait(false);

                await appDbContext.Users
                    .ExecuteUpdateAsync(user => user.SetProperty(u => u.CdnExpireAt, u => u.CdnExpireAt + seconds))
                    .ConfigureAwait(false);
            }

            nowTick += seconds;
            return DateTimeOffset.FromUnixTimeSeconds(nowTick);
        }
    }
}