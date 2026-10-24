using Microsoft.EntityFrameworkCore.Storage;
using server.Core;
using server.Extension;
using server.Models.Entity.Redeem;
using server.Models.Redeem;

namespace server.Services.Redeem;

// Scoped
public sealed class RedeemService(IServiceProvider serviceProvider, TimeProvider timeProvider)
{
    private static readonly AsyncLock RedeemLock = new();
    private readonly GachaLogExpireService _gachaLogExpireService = serviceProvider.GetRequiredService<GachaLogExpireService>();
    private readonly CdnExpireService _cdnExpireService = serviceProvider.GetRequiredService<CdnExpireService>();
    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();

    public async Task<RedeemUseResponse> UseRedeemCodeAsync(RedeemUseRequest request)
    {
        using (await RedeemLock.LockAsync().ConfigureAwait(false))
        {
            ArgumentNullException.ThrowIfNull(request.Username);
            if (request.Code.Length is not 18)
            {
                return new(RedeemStatus.Invalid);
            }

            if (await _appDbContext.RedeemCodes.Where(c => c.Code == request.Code).SingleOrDefaultAsync().ConfigureAwait(false) is not { } code)
            {
                return new(RedeemStatus.NotExists);
            }

            string normalizedUsername = request.Username.ToUpperInvariant();
            if (await _appDbContext.Users.Where(u => u.UserName == normalizedUsername).SingleOrDefaultAsync().ConfigureAwait(false) is not { } user)
            {
                return new(RedeemStatus.NoSuchUser);
            }

            int userId = user.Id;
            if (await _appDbContext.RedeemCodeUseItems.AnyAsync(i => i.RedeemCodeId == code.Id && i.UsedBy == userId).ConfigureAwait(false))
            {
                return new(RedeemStatus.AlreadyUsed);
            }

            if (code.Type.HasFlag(RedeemCodeType.TimeLimited))
            {
                if (code.ExpireTime <= timeProvider.GetUtcNow())
                {
                    return new(RedeemStatus.Expired);
                }
            }

            if (code.Type.HasFlag(RedeemCodeType.TimesLimited))
            {
                if (code.TimesAllowed <= await _appDbContext.RedeemCodeUseItems.Where(i => i.RedeemCodeId == code.Id).CountAsync().ConfigureAwait(false))
                {
                    return new(RedeemStatus.NotEnough);
                }
            }

            return await ExtendTermForUserByCodeAsync(user, code).ConfigureAwait(false);
        }
    }

    public async Task<RedeemGenerateResponse> GenerateRedeemCodesAsync(RedeemGenerateRequest req)
    {
        List<string> codes = [];
        for (uint i = 0; i < req.Count; i++)
        {
            RedeemCode code = new()
            {
                Code = Guid.NewGuid().ToString().ToUpperInvariant()[..18],
                Type = req.Type,
                ServiceType = req.ServiceType,
                Value = req.Value,
                Description = req.Description,
                Creator = req.Creator,
                CreateTime = timeProvider.GetUtcNow(),
            };

            if (code.Type.HasFlag(RedeemCodeType.TimeLimited))
            {
                code.ExpireTime = req.ExpireTime;
            }

            if (code.Type.HasFlag(RedeemCodeType.TimesLimited))
            {
                code.TimesAllowed = req.Times;
            }

            // Avoid generating duplicate code
            if (!await _appDbContext.RedeemCodes.AnyAsync(c => c.Code == code.Code).ConfigureAwait(false))
            {
                codes.Add(code.Code);
                await _appDbContext.RedeemCodes.AddAndSaveAsync(code).ConfigureAwait(false);
            }
        }

        return new(codes);
    }

    private async ValueTask<RedeemUseResponse> ExtendTermForUserByCodeAsync(HutaoUser user, RedeemCode code)
    {
        using IDbContextTransaction transaction = await _appDbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        IExpireService expireService = code.ServiceType switch
        {
            RedeemCodeTargetServiceType.GachaLog => _gachaLogExpireService,
            RedeemCodeTargetServiceType.Cdn => _cdnExpireService,
            _ => throw new NotSupportedException(),
        };

        TermExtendResult result = await expireService.ExtendTermForUserAsync(_appDbContext.Users, user, code.Value).ConfigureAwait(false);
        if (result.Kind is TermExtendResultKind.NoSuchUser)
        {
            return new(RedeemStatus.NoSuchUser);
        }

        if (result.Kind is TermExtendResultKind.DbError)
        {
            return new(RedeemStatus.DbError);
        }

        RedeemCodeUseItem useItem = new()
        {
            RedeemCodeId = code.Id,
            UsedBy = user.Id,
            UseTime = timeProvider.GetUtcNow(),
        };

        await _appDbContext.RedeemCodeUseItems.AddAndSaveAsync(useItem);
        await transaction.CommitAsync().ConfigureAwait(false);

        return new(RedeemStatus.Ok, code.ServiceType, code.Value, result.ExpiredAt);
    }
}