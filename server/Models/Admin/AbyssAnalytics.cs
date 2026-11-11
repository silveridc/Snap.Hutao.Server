namespace server.Models.Admin;

/// <summary>两个已支持的深渊玩法：深境螺旋与幻想真境剧诗.</summary>
public sealed record AbyssAnalytics(AbyssModeAnalytics SpiralAbyss, AbyssModeAnalytics Theater);
