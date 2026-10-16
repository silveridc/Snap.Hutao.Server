namespace server.Tests.Infrastructure;

/// <summary>
/// 所有接口测试共享同一个进程内宿主：MySQL 测试库与 Garnet 的启动成本较高，
/// 复用宿主可以显著缩短测试时间。需要独立数据的测试请使用各自的用户/数据。
/// </summary>
[CollectionDefinition(Name)]
public sealed class HutaoServerCollection : ICollectionFixture<HutaoServerFactory>
{
    public const string Name = "hutao-server";
}
