using server.Models.Admin;
using server.Models.Legacy;
using server.Models.Upload;

namespace server.Services.Ranking;

public interface IRankService
{
    public Task<long> ClearRanksAsync();

    public Task<Rank> RetriveRankAsync(string uid);

    public Task SaveRankAsync(string uid, SimpleRank damage, SimpleRank? takeDamage);

    public Task<List<string>> GetRankKeysAsync();

    public Task<List<RankEntry>> GetLeaderboardAsync(string key, int count);
}
