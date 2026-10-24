using System.Linq.Expressions;
using server.Extension;

namespace server.Services.Expire;

// Singleton
public sealed class CdnExpireService(IServiceProvider serviceProvider, TimeProvider timeProvider)
    : ExpireServiceBase(serviceProvider, timeProvider)
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
