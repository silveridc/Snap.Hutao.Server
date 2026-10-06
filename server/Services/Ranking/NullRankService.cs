using server.Models.Admin;
using server.Models.Legacy;
using server.Models.Upload;

namespace server.Services.Ranking
{
    internal sealed class NullRankService : IRankService
    {
        public Task<long> ClearRanksAsync()
        {
            return Task.FromResult(0L);
        }

        public Task<Rank> RetriveRankAsync(string uid)
        {
            return Task.FromResult(default(Rank)!);
        }

        public Task SaveRankAsync(string uid, SimpleRank damage, SimpleRank? takeDamage)
        {
            return Task.CompletedTask;
        }

        public Task<List<string>> GetRankKeysAsync()
        {
            return Task.FromResult(new List<string>());
        }

        public Task<List<RankEntry>> GetLeaderboardAsync(string key, int count)
        {
            return Task.FromResult(new List<RankEntry>());
        }
    }
}