using System.Linq.Expressions;

namespace server.Services.Expire;

public abstract class ExpireServiceBase(IServiceProvider serviceProvider, TimeProvider timeProvider) : IExpireService
{
    /// <summary>EF 翻译用的到期字段选择器（如 u =&gt; u.CdnExpireAt）。</summary>
    protected abstract Expression<Func<HutaoUser, long>> ExpireAtSelector { get; }

    protected abstract long GetExpireAt(HutaoUser user);

    protected abstract void SetExpireAt(HutaoUser user, long expireAt);

    public async ValueTask<TermExtendResult> ExtendTermForUserNameAsync(string userName, int days)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        UserManager<HutaoUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<HutaoUser>>();

        HutaoUser? user = await userManager.FindByNameAsync(userName).ConfigureAwait(false);
        if (user == null)
        {
            return new(TermExtendResultKind.NoSuchUser);
        }

        long expireAt = ExtendFrom(timeProvider.GetUnixTimeSeconds(), GetExpireAt(user), days);
        SetExpireAt(user, expireAt);

        IdentityResult result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        return result.Succeeded
            ? new(TermExtendResultKind.Ok, DateTimeOffset.FromUnixTimeSeconds(expireAt))
            : new(TermExtendResultKind.DbError);
    }

    public async ValueTask<TermExtendResult> ExtendTermForUserAsync(DbSet<HutaoUser> users, HutaoUser user, int days)
    {
        long expireAt = ExtendFrom(timeProvider.GetUnixTimeSeconds(), GetExpireAt(user), days);
        SetExpireAt(user, expireAt);

        int result = await users.UpdateAndSaveAsync(user).ConfigureAwait(false);
        return result is 0
            ? new(TermExtendResultKind.DbError)
            : new(TermExtendResultKind.Ok, DateTimeOffset.FromUnixTimeSeconds(expireAt));
    }

    public async ValueTask<DateTimeOffset> ExtendTermForAllUsersAsync(int days)
    {
        long now = timeProvider.GetUnixTimeSeconds();
        long seconds = Duration(days);

        // 属性每次访问都会新建表达式树；必须只取一次，保证 Body 与参数出自同一棵树
        Expression<Func<HutaoUser, long>> selector = ExpireAtSelector;
        ParameterExpression user = selector.Parameters[0];

        using IServiceScope scope = serviceProvider.CreateScope();
        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 过期的抬到当前时刻，随后统一加 days——与单用户路径的 max(expire, now) + days 同一语义
        await appDbContext.Users
            .Where(Expression.Lambda<Func<HutaoUser, bool>>(Expression.LessThan(selector.Body, Expression.Constant(now)), user))
            .ExecuteUpdateAsync(setters => setters.SetProperty(selector, _ => now))
            .ConfigureAwait(false);

        await appDbContext.Users
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                selector,
                Expression.Lambda<Func<HutaoUser, long>>(Expression.Add(selector.Body, Expression.Constant(seconds)), user)))
            .ConfigureAwait(false);

        return DateTimeOffset.FromUnixTimeSeconds(now + seconds);
    }

    private static long ExtendFrom(long now, long expireAt, int days)
    {
        return Math.Max(expireAt, now) + Duration(days);
    }

    private static long Duration(int days)
    {
        return (long)TimeSpan.FromDays(days).TotalSeconds;
    }
}
