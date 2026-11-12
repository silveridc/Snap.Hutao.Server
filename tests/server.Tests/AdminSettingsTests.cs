using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class AdminSettingsTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task ConfigUpdate_ThenReset_RestoresTheDefault()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string key = $"HutaoTest.Config.{Guid.NewGuid():N}";

        TestResponse updated = await client.SendEnvelopeAsync("PATCH", "/api/admin/settings", JsonSerializer.Serialize(new Dictionary<string, string?> { [key] = "42" }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse fetched = await client.SendEnvelopeAsync("GET", "/api/admin/settings", null, maintainer.AccessToken);
        Assert.Equal("42", fetched.DeserializeData<Dictionary<string, string?>>()![key]);

        TestResponse reset = await client.SendEnvelopeAsync("DELETE", $"/api/admin/settings/{key}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, reset.Retcode);

        TestResponse afterReset = await client.SendEnvelopeAsync("GET", "/api/admin/settings", null, maintainer.AccessToken);
        Assert.DoesNotContain(key, afterReset.DeserializeData<Dictionary<string, string?>>()!.Keys);
    }

    [Fact]
    public async Task ConfigUpdate_WithEmptyBody_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("PATCH", "/api/admin/settings", "{}", maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task AllowedVersionAdd_ThenRemove_RoundTrips()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string header = $"Integration Hutao/{Guid.NewGuid():N}";

        TestResponse added = await client.SendEnvelopeAsync("POST", $"/api/admin/allowed-client-versions?header={Uri.EscapeDataString(header)}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, added.Retcode);

        TestResponse list = await client.SendEnvelopeAsync("GET", "/api/admin/allowed-client-versions", null, maintainer.AccessToken);
        Assert.Contains(header, list.DeserializeData<List<string>>()!);

        TestResponse removed = await client.SendEnvelopeAsync("DELETE", $"/api/admin/allowed-client-versions?header={Uri.EscapeDataString(header)}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, removed.Retcode);

        TestResponse afterRemove = await client.SendEnvelopeAsync("GET", "/api/admin/allowed-client-versions", null, maintainer.AccessToken);
        Assert.DoesNotContain(header, afterRemove.DeserializeData<List<string>>()!);
    }

    [Fact]
    public async Task CacheInvalidate_ReportsRemovedKeys()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("DELETE", "/api/admin/cache", JsonSerializer.Serialize(new
        {
            Keys = new[] { "HutaoTest.Missing" },
            Patterns = new[] { "HutaoTest.Missing:*" },
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(1, parsed.Data.GetProperty("RemovedKeys").GetInt32());
        Assert.Equal(0, parsed.Data.GetProperty("RemovedByPattern").GetInt64());
    }
}
