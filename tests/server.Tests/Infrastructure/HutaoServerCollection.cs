namespace server.Tests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class HutaoServerCollection : ICollectionFixture<HutaoServerFactory>
{
    public const string Name = "hutao-server";
}
