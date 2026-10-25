using System.Runtime.InteropServices;
using server.Extension;
using server.Models.Entity.GachaLog;
using server.Models.Metadata;
using server.Services.Legacy.Primitive;

namespace server.Services.GachaLog.Statistics;

// Transient
public sealed class GachaLogStatisticsService(IServiceProvider serviceProvider, TimeProvider timeProvider)
{
    public const string Working = "GachaLogStatisticsService.Working";

    // Compile queries that used multiple times to increase performance
    // SELECT DISTINCT "g"."Uid"
    // FROM "GachaItems" AS "g"
    private static readonly Func<AppDbContext, IEnumerable<string>> UidsQuery = EF.CompileQuery((AppDbContext context) =>
        context.GachaItems.AsNoTracking().Select(g => g.Uid).Distinct());

    // .AsQueryable() make sure the compiler use the correct overload.
    // SELECT "g"."UserId", "g"."Uid", "g"."Id", "g"."IsTrusted", "g"."GachaType", "g"."QueryType", "g"."ItemId", "g"."Time"
    // FROM "GachaItems" AS "g"
    // WHERE "g"."Uid" = @__uid_1
    // ORDER BY "g"."Id"
    private static readonly Func<AppDbContext, string, IEnumerable<EntityGachaItem>> GachaItemsQuery = EF.CompileQuery((AppDbContext context, string uid) =>
        context.GachaItems.AsNoTracking().Where(g => g.Uid == uid).OrderBy(g => g.Id).AsQueryable());

    private readonly AppDbContext _appDbContext = serviceProvider.GetRequiredService<AppDbContext>();
    private readonly MetadataDbContext _metadataDbContext = serviceProvider.GetRequiredService<MetadataDbContext>();
    private readonly IDistributedCache _cache = serviceProvider.GetRequiredService<IDistributedCache>();

    public async Task RunAsync()
    {
        await _cache.RunExclusiveAsync(Working, async () =>
        {
            Map<int, int> idQualityMap = await GetIdQualityMapAsync();
            GachaEventBundle bundle = await GetCurrentGachaEventAsync();

            // If the bundle is not complete, don't run the statistics
            if (bundle is not { AvatarEvent1: { }, AvatarEvent2: { }, WeaponEvent: { } })
            {
                return;
            }

            GachaLogStatisticsTracker tracker = new(idQualityMap, bundle);

            await Task.Run(() => RunCore(tracker)).ConfigureAwait(false);
            _ = tracker.CompleteTracking(_appDbContext, _cache);
        }).ConfigureAwait(false);
    }

    private async Task<Map<int, int>> GetIdQualityMapAsync()
    {
        List<KnownItem> knownItems = await _metadataDbContext.KnownItems.AsNoTracking().ToListAsync().ConfigureAwait(false);
        return new(knownItems.ToDictionary(x => (int)x.Id, x => (int)x.Quality));
    }

    private async Task<GachaEventBundle> GetCurrentGachaEventAsync()
    {
        DateTime now = timeProvider.GetLocalNow().DateTime;

        GachaEventInfo? avatarEvent1 = await _metadataDbContext.GachaEvents
            .FirstOrDefaultAsync(g => g.From < now && g.To > now && g.Type == GachaConfigType.ActivityAvatar)
            .ConfigureAwait(false);

        GachaEventInfo? avatarEvent2 = await _metadataDbContext.GachaEvents
            .FirstOrDefaultAsync(g => g.From < now && g.To > now && g.Type == GachaConfigType.SpeicalActivityAvatar)
            .ConfigureAwait(false);

        GachaEventInfo? weaponEvent = await _metadataDbContext.GachaEvents
            .FirstOrDefaultAsync(g => g.From < now && g.To > now && g.Type == GachaConfigType.ActivityWeapon)
            .ConfigureAwait(false);

        GachaEventInfo? chronicled = await _metadataDbContext.GachaEvents
            .FirstOrDefaultAsync(g => g.From < now && g.To > now && g.Type == GachaConfigType.ActivityCity)
            .ConfigureAwait(false);

        GachaEventBundle bundle = new()
        {
            AvatarEvent1 = avatarEvent1,
            AvatarEvent2 = avatarEvent2,
            WeaponEvent = weaponEvent,
            Chronicled = chronicled,
        };

        return bundle;
    }

    private void RunCore(GachaLogStatisticsTracker tracker)
    {
        List<string> uids = [.. UidsQuery(_appDbContext)];
        HashSet<string> invalidUids = [.. _appDbContext.InvalidGachaUids.Select(i => i.Uid)];
        foreach (ref string uid in CollectionsMarshal.AsSpan(uids))
        {
            if (invalidUids.Contains(uid))
            {
                continue;
            }

            tracker.Uid = uid;

            // 按 Id 递增
            List<EntityGachaItem> gachaItems = [.. GachaItemsQuery(_appDbContext, uid)];
            tracker.Track(gachaItems);
        }
    }
}
