using server.Models.Legacy;
using server.Services.Legacy.Primitive;

namespace server.Services.Legacy
{
    public static class StatisticsTrackerHelper
    {
        public static AvatarUsageRank AvatarUsageRank(int floor, Map<AvatarId, int> counter, Map<AvatarId, int> recordCounter)
        {
            return new()
            {
                Floor = floor,
                Ranks = [.. counter.Select(kvp => new ItemRate<int, double>(kvp.Key, kvp.Value / (double)recordCounter[kvp.Key]))],
            };
        }

        public static AvatarAppearanceRank AvatarAppearanceRank(int floor, Map<AvatarId, int> counter, int levelRecordCount)
        {
            return new()
            {
                Floor = floor,
                Ranks = [.. counter.Select(kvp => new ItemRate<int, double>(kvp.Key, kvp.Value / (double)levelRecordCount))],
            };
        }

        public static AvatarConstellationInfo AvatarConstellationInfo(AvatarId avatarId, int totalRecord, int avatarCount, Map<Constellation, int> constellationCounter)
        {
            return new(avatarId)
            {
                HoldingRate = (double)avatarCount / totalRecord,
                Constellations = [.. constellationCounter.Select(kvp => new ItemRate<int, double>(kvp.Key, kvp.Value / (double)avatarCount))],
            };
        }

        public static AvatarCollocation AvatarCollocation(AvatarId avatarId, Map<AvatarId, int> avatarBuildCounter, Map<WeaponId, int> weaponBuildCounter, Map<ReliquarySets, int> reliquaryBuildCounter)
        {
            double coAvatarTotalCount = avatarBuildCounter.Sum(kvp => kvp.Value);
            double weaponTotalCount = weaponBuildCounter.Sum(kvp => kvp.Value);
            double reliquarySetTotalCount = reliquaryBuildCounter.Sum(kvp => kvp.Value);

            return new(avatarId)
            {
                Avatars = [.. avatarBuildCounter
                    .OrderByDescending(x => x.Value)
                    .Take(16)
                    .Select(kvp => new ItemRate<int, double>(kvp.Key, kvp.Value / coAvatarTotalCount))],
                Weapons = [.. weaponBuildCounter
                    .OrderByDescending(x => x.Value)
                    .Take(16)
                    .Select(kvp => new ItemRate<int, double>(kvp.Key, kvp.Value / weaponTotalCount))],
                Reliquaries = [.. reliquaryBuildCounter
                    .OrderByDescending(x => x.Value)
                    .Take(16)
                    .Select(kvp => new ItemRate<string, double>(kvp.Key, kvp.Value / reliquarySetTotalCount))],
            };
        }

        public static WeaponCollocation WeaponCollocation(WeaponId weaponId, Map<AvatarId, int> avatarBuildCounter)
        {
            double coAvatarTotalCount = avatarBuildCounter.Sum(kvp => kvp.Value);

            return new(weaponId)
            {
                Avatars = [.. avatarBuildCounter
                    .OrderByDescending(x => x.Value)
                    .Take(16)
                    .Select(kvp => new ItemRate<int, double>(kvp.Key, kvp.Value / coAvatarTotalCount))],
            };
        }

        public static TeamAppearance TeamAppearance(int floor, Map<Team, int> upTeamCounter, Map<Team, int> downTeamCounter)
        {
            return new()
            {
                Floor = floor,
                Up = [.. upTeamCounter
                    .OrderByDescending(x => x.Value)
                    .Take(48)
                    .Select(kvp => new ItemRate<string, int>(kvp.Key, kvp.Value))],
                Down = [.. downTeamCounter
                    .OrderByDescending(x => x.Value)
                    .Take(48)
                    .Select(kvp => new ItemRate<string, int>(kvp.Key, kvp.Value))],
            };
        }

        public static void AddTeamAvatarToHashSet(Team team, HashSet<AvatarId> set)
        {
            if (team.Count == 4)
            {
                set.Add(team.Position1);
                set.Add(team.Position2);
                set.Add(team.Position3);
                set.Add(team.Position4);
            }
            else if (team.Count == 3)
            {
                set.Add(team.Position1);
                set.Add(team.Position2);
                set.Add(team.Position3);
            }
            else if (team.Count == 2)
            {
                set.Add(team.Position1);
                set.Add(team.Position2);
            }
            else if (team.Count == 1)
            {
                set.Add(team.Position1);
            }
        }

        public static Team ToTeam(List<int> avatars)
        {
            return new(avatars);
        }
    }
}