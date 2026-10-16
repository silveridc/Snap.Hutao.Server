using server.Extension;
using server.Models.Entity.GachaLog;
using server.Models.GachaLog;
using server.Models.Metadata;
using server.Services.Legacy.Primitive;
using System.Runtime.InteropServices;

namespace server.Services.GachaLog.Statistics
{
    internal sealed class GachaLogStatisticsTracker(Map<int, int> idQualityMap, GachaEventBundle bundle)
    {
        private readonly GachaEventInfo? currentAvatarEvent1 = bundle.AvatarEvent1;
        private readonly GachaEventInfo? currentAvatarEvent2 = bundle.AvatarEvent2;
        private readonly GachaEventInfo? currentWeaponEvent = bundle.WeaponEvent;
        private readonly GachaEventInfo? currentChronicled = bundle.Chronicled;
        private readonly FrozenSet<int>? currentAvatarEvent1Star5Ids = bundle.AvatarEvent1?.GetUpOrangeItems().ToFrozenSet();
        private readonly FrozenSet<int>? currentAvatarEvent2Star5Ids = bundle.AvatarEvent2?.GetUpOrangeItems().ToFrozenSet();
        private readonly FrozenSet<int>? currentWeaponEventStar5Ids = bundle.WeaponEvent?.GetUpOrangeItems().ToFrozenSet();
        private readonly FrozenSet<int>? currentChronicledStar5Ids = bundle.Chronicled?.GetUpOrangeItems().ToFrozenSet();

        private readonly HashSet<string> invalidGachaUids = [];

        // itemId -> count
        private readonly Map<int, long> currentAvatarEvent1Counter = [];
        private readonly Map<int, long> currentAvatarEvent2Counter = [];
        private readonly Map<int, long> currentWeaponEventCounter = [];
        private readonly Map<int, long> currentChronicledCounter = [];

        // pulls -> count
        private readonly Map<int, long> avatarEventStar5Distribution = [];
        private readonly Map<int, long> weaponEventStar5Distribution = [];
        private readonly Map<int, long> standardStar5Distribution = [];
        private readonly Map<int, long> chronicledStar5Distribution = [];

        private long totalMixedPullsCounter;
        private long totalAvatarEventValidPullsCounter;
        private long totalWeaponEventValidPullsCounter;
        private long totalChronicledValidPullsCounter;
        private long totalStandardValidPullsCounter;

        /// <summary>
        /// 当前处理的 Uid
        /// </summary>
        public string Uid { get; set; } = string.Empty;

        /// <summary>
        /// 追踪记录
        /// </summary>
        /// <param name="gachaItems">祈愿记录</param>
        public void Track(List<EntityGachaItem> gachaItems)
        {
            // Ignore items before first star5 item
            bool avatarEventFirstStar5Found = false;
            bool weaponEventFirstStar5Found = false;
            bool chronicledStar5Found = false;
            bool standardFirstStar5Found = false;

            int currentAvatarEventCountToLastStar5 = 0;
            int currentWeaponEventCountToLastStar5 = 0;
            int currentChronicledCountToLastStar5 = 0;
            int currentStandardCountToLastStar5 = 0;

            foreach (ref readonly EntityGachaItem item in CollectionsMarshal.AsSpan(gachaItems))
            {
                ++totalMixedPullsCounter;

                switch (item.QueryType)
                {
                    case GachaConfigType.ActivityAvatar:
                        TrackForSpecficQueryTypeWish(
                            item,
                            ref avatarEventFirstStar5Found,
                            ref currentAvatarEventCountToLastStar5,
                            ref totalAvatarEventValidPullsCounter,
                            avatarEventStar5Distribution,
                            90);
                        break;
                    case GachaConfigType.ActivityWeapon:
                        TrackForSpecficQueryTypeWish(
                            item,
                            ref weaponEventFirstStar5Found,
                            ref currentWeaponEventCountToLastStar5,
                            ref totalWeaponEventValidPullsCounter,
                            weaponEventStar5Distribution,
                            80);
                        break;
                    case GachaConfigType.ActivityCity:
                        TrackForSpecficQueryTypeWish(
                            item,
                            ref chronicledStar5Found,
                            ref currentChronicledCountToLastStar5,
                            ref totalChronicledValidPullsCounter,
                            chronicledStar5Distribution,
                            90);
                        break;
                    case GachaConfigType.Standard:
                        TrackForSpecficQueryTypeWish(
                            item,
                            ref standardFirstStar5Found,
                            ref currentStandardCountToLastStar5,
                            ref totalStandardValidPullsCounter,
                            standardStar5Distribution,
                            90);
                        break;
                }
            }
        }

        public GachaEventStatistics CompleteTracking(AppDbContext appDbContext, IDistributedCache cache)
        {
            if (invalidGachaUids.Count != 0)
            {
                appDbContext.InvalidGachaUids.AddRangeAndSave(invalidGachaUids.Select(uid => new InvalidGachaUid() { Uid = uid }));

                return new()
                {
                    Status = GachaEventStatisticsStatus.InvalidItemDetected,
                    InvalidUids = invalidGachaUids,
                    PullsEnumerated = totalMixedPullsCounter,
                };
            }

            // Avatar
            GachaDistribution avatarEventDistribution = new()
            {
                TotalValidPulls = totalAvatarEventValidPullsCounter,
                Distribution = [.. avatarEventStar5Distribution.Select(kvp => new PullCount() { Pull = kvp.Key, Count = kvp.Value })],
            };

            SaveStatistics(appDbContext, cache, GachaStatistics.AvaterEventGachaDistribution, avatarEventDistribution);

            // Weapon
            GachaDistribution weaponEventDistribution = new()
            {
                TotalValidPulls = totalWeaponEventValidPullsCounter,
                Distribution = [.. weaponEventStar5Distribution.Select(kvp => new PullCount() { Pull = kvp.Key, Count = kvp.Value })],
            };

            SaveStatistics(appDbContext, cache, GachaStatistics.WeaponEventGachaDistribution, weaponEventDistribution);

            // Chronicled
            GachaDistribution chronicledDistribution = new()
            {
                TotalValidPulls = totalChronicledValidPullsCounter,
                Distribution = [.. chronicledStar5Distribution.Select(kvp => new PullCount() { Pull = kvp.Key, Count = kvp.Value })],
            };

            SaveStatistics(appDbContext, cache, GachaStatistics.ChronicledGachaDistribution, chronicledDistribution);

            // Standard
            GachaDistribution standardDistribution = new()
            {
                TotalValidPulls = totalStandardValidPullsCounter,
                Distribution = [.. standardStar5Distribution.Select(kvp => new PullCount() { Pull = kvp.Key, Count = kvp.Value })],
            };

            SaveStatistics(appDbContext, cache, GachaStatistics.StandardGachaDistribution, standardDistribution);

            GachaEventStatistics statistics = new()
            {
                Status = GachaEventStatisticsStatus.Ok,
                AvatarEvent = [.. currentAvatarEvent1Counter.Select(kvp => new ItemCount() { Item = kvp.Key, Count = kvp.Value })],
                AvatarEvent2 = [.. currentAvatarEvent2Counter.Select(kvp => new ItemCount() { Item = kvp.Key, Count = kvp.Value })],
                WeaponEvent = [.. currentWeaponEventCounter.Select(kvp => new ItemCount() { Item = kvp.Key, Count = kvp.Value })],
                Chronicled = [.. currentChronicledCounter.Select(kvp => new ItemCount() { Item = kvp.Key, Count = kvp.Value })],
                PullsEnumerated = totalMixedPullsCounter,
            };

            SaveStatistics(appDbContext, cache, GachaStatistics.GachaEventStatistics, statistics);
            return statistics;
        }

        private static void SaveStatistics<T>(AppDbContext appDbContext, IDistributedCache cache, string name, T data)
        {
            GachaStatistics? statistics = appDbContext.GachaStatistics
                .SingleOrDefault(s => s.Name == name);

            if (statistics == null)
            {
                statistics = GachaStatistics.CreateWithName(name);
                appDbContext.GachaStatistics.Add(statistics);
            }

            cache.Set(name, data);
            statistics.Data = JsonSerializer.Serialize(data);

            appDbContext.SaveChanges();
        }

        private static bool ImpossiblePresence(FrozenSet<int> upStar5Items, EntityGachaItem item, int quality)
        {
            // TODO: Handle quality 4
            if (quality is not 5)
            {
                return false;
            }

            if (upStar5Items.Contains(item.ItemId))
            {
                return false;
            }

            if (SpecializedGachaLogItems.IsStandardWishItem(item))
            {
                return false;
            }

            return true;
        }

        private void TrackForSpecficQueryTypeWish(EntityGachaItem item, ref bool star5Found, ref int lastStar5Counter, ref long totalPullsCounter, Map<int, long> distribution, int pullMaxThreshold)
        {
            switch (idQualityMap[item.ItemId])
            {
                case 3:
                    if (star5Found)
                    {
                        ++lastStar5Counter;
                        ++totalPullsCounter;
                        TryIncreaseCurrentWishItemCounter(item, 3);
                    }

                    break;
                case 4:
                    if (star5Found)
                    {
                        ++lastStar5Counter;
                        ++totalPullsCounter;
                        TryIncreaseCurrentWishItemCounter(item, 4);
                    }

                    break;
                case 5:
                    if (star5Found)
                    {
                        ++lastStar5Counter;
                        ++totalPullsCounter;
                        TryIncreaseCurrentWishItemCounter(item, 5);

                        if (lastStar5Counter > pullMaxThreshold)
                        {
                            invalidGachaUids.Add(Uid);
                        }

                        distribution.IncreaseOne(lastStar5Counter);

                        lastStar5Counter = 0; // reset
                    }

                    star5Found = true;
                    break;
            }
        }

        private void TryIncreaseCurrentWishItemCounter(EntityGachaItem item, int quality)
        {
            if (currentAvatarEvent1 is not null && currentAvatarEvent1.ItemInThisEvent(item))
            {
                currentAvatarEvent1Counter.IncreaseOne(item.ItemId);

                if (ImpossiblePresence(currentAvatarEvent1Star5Ids!, item, quality))
                {
                    invalidGachaUids.Add(Uid);
                }
            }
            else if (currentAvatarEvent2 is not null && currentAvatarEvent2.ItemInThisEvent(item))
            {
                currentAvatarEvent2Counter.IncreaseOne(item.ItemId);

                if (ImpossiblePresence(currentAvatarEvent2Star5Ids!, item, quality))
                {
                    invalidGachaUids.Add(Uid);
                }
            }
            else if (currentWeaponEvent is not null && currentWeaponEvent.ItemInThisEvent(item))
            {
                currentWeaponEventCounter.IncreaseOne(item.ItemId);

                if (ImpossiblePresence(currentWeaponEventStar5Ids!, item, quality))
                {
                    invalidGachaUids.Add(Uid);
                }
            }
            else if (currentChronicled is not null && currentChronicled.ItemInThisEvent(item))
            {
                currentChronicledCounter.IncreaseOne(item.ItemId);

                if (ImpossiblePresence(currentChronicledStar5Ids!, item, quality))
                {
                    invalidGachaUids.Add(Uid);
                }
            }
        }
    }
}