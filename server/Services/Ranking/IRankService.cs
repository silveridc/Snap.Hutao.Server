using server.Models.Admin;
using server.Models.Legacy;
using server.Models.Upload;

namespace server.Services.Ranking
{
    public interface IRankService
    {
        Task<long> ClearRanksAsync();

        Task<Rank> RetriveRankAsync(string uid);

        Task SaveRankAsync(string uid, SimpleRank damage, SimpleRank? takeDamage);

        Task<List<string>> GetRankKeysAsync();

        Task<List<RankEntry>> GetLeaderboardAsync(string key, int count);
    }
}