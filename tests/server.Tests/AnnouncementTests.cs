using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class AnnouncementTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task AnnouncementLifecycle_UploadUpdateDisableDelete()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string title = $"integration-{Guid.NewGuid():N}";

        TestResponse uploaded = await client.SendEnvelopeAsync("POST", "/api/admin/announcements", JsonSerializer.Serialize(new
        {
            Title = title,
            Content = "integration content",
            Severity = 0,
            Link = "https://example.com",
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, uploaded.Retcode);

        long id = await FindAnnouncementIdAsync(maintainer, title);
        Assert.True(id > 0, "The uploaded announcement should appear in the admin list");

        TestResponse updated = await client.SendEnvelopeAsync("PUT", $"/api/admin/announcements/{id}", JsonSerializer.Serialize(new
        {
            Id = id,
            Locale = "ALL",
            Title = title,
            Content = "updated content",
            Severity = 2,
            Link = "https://example.com",
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, updated.Retcode);

        TestResponse disabled = await client.SendEnvelopeAsync("PATCH", $"/api/admin/announcements/{id}?enabled=false", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, disabled.Retcode);

        TestResponse deleted = await client.SendEnvelopeAsync("DELETE", $"/api/admin/announcements/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, deleted.Retcode);

        // 删除后同一条公告再删一次应报不存在
        TestResponse again = await client.SendEnvelopeAsync("DELETE", $"/api/admin/announcements/{id}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.NotFound, again.Retcode);
    }

    [Theory]
    [InlineData("", "content")]
    [InlineData("title", "")]
    [InlineData("   ", "content")]
    public async Task AnnouncementUpdate_WithoutTitleOrContent_IsRejected(string title, string content)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("PUT", "/api/admin/announcements/1", JsonSerializer.Serialize(new
        {
            Id = 1,
            Locale = "ALL",
            Title = title,
            Content = content,
            Link = "https://example.com",
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    private async Task<long> FindAnnouncementIdAsync(TestUser maintainer, string title)
    {
        TestResponse list = await client.SendEnvelopeAsync("GET", "/api/admin/announcements?page=1&pageSize=100", null, maintainer.AccessToken);
        PagedResult<server.Models.Entity.Announcement.EntityAnnouncement>? page =
            list.DeserializeData<PagedResult<server.Models.Entity.Announcement.EntityAnnouncement>>();

        return page?.Items.FirstOrDefault(item => item.Title == title)?.Id ?? 0;
    }
}
