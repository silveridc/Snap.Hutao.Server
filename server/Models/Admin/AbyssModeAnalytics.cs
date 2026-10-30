namespace server.Models.Admin;

public sealed record AbyssModeAnalytics(
    long Records,
    int UniqueUids,
    long FullStarRecords,
    IReadOnlyList<AbyssFloorAnalytics> Floors,
    IReadOnlyList<AvatarRank> TopAvatars);
