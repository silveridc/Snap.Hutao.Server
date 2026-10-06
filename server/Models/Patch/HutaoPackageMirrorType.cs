namespace server.Models.Patch
{
    /// <summary>
    /// 镜像类型，序列化为字符串以匹配客户端（Direct/Archive/Browser）。
    /// </summary>
    public enum HutaoPackageMirrorType
    {
        Direct,
        Archive,
        Browser,
    }
}
