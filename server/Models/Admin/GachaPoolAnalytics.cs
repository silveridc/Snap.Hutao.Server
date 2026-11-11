namespace server.Models.Admin;

public sealed record GachaPoolAnalytics(int GachaType, long Pulls, long Star5, double Star5Rate, double AveragePullsPerStar5);
