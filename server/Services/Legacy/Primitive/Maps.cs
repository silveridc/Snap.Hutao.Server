namespace server.Services.Legacy.Primitive
{
    /// <summary>
    /// 映射相关
    /// </summary>
    public static class Maps
    {
        internal static Map<Constellation, int> ForConstellation(AvatarId avatarId)
        {
            _ = avatarId;
            return new()
            {
                [0] = 0,
                [1] = 0,
                [2] = 0,
                [3] = 0,
                [4] = 0,
                [5] = 0,
                [6] = 0,
            };
        }
    }
}