using System.Linq.Expressions;
using server.Extension;

namespace server.Services.Expire;

// Singleton
public sealed class GachaLogExpireService(IServiceProvider serviceProvider, TimeProvider timeProvider)
    : ExpireServiceBase(serviceProvider, timeProvider)
{
    protected override Expression<Func<HutaoUser, long>> ExpireAtSelector => user => user.GachaLogExpireAt;

    protected override long GetExpireAt(HutaoUser user)
    {
        return user.GachaLogExpireAt;
    }

    protected override void SetExpireAt(HutaoUser user, long expireAt)
    {
        user.GachaLogExpireAt = expireAt;
    }
}
