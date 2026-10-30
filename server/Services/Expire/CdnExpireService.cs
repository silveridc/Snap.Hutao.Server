using System.Linq.Expressions;

namespace server.Services.Expire;

// Singleton
public sealed class CdnExpireService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    : ExpireServiceBase(scopeFactory, timeProvider)
{
    protected override Expression<Func<HutaoUser, long>> ExpireAtSelector => user => user.CdnExpireAt;

    protected override long GetExpireAt(HutaoUser user)
    {
        return user.CdnExpireAt;
    }

    protected override void SetExpireAt(HutaoUser user, long expireAt)
    {
        user.CdnExpireAt = expireAt;
    }
}
