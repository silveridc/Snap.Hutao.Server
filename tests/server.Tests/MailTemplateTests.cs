using server.Models.Admin;
using server.Services.Mail;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class MailTemplateTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task MailTemplate_SaveThenReset_RestoresTheDefault()
    {
        TestUser maintainer = await users.MaintainerAsync();

        // 先清掉可能残留的自定义模板，保证初始状态是默认模板
        await client.SendEnvelopeAsync("DELETE", "/api/admin/mail/template", null, maintainer.AccessToken);

        TestResponse initial = await client.SendEnvelopeAsync("GET", "/api/admin/mail/template", null, maintainer.AccessToken);
        MailTemplatePayload initialPayload = initial.DeserializeData<MailTemplatePayload>()!;
        Assert.False(initialPayload.Customized);
        Assert.Equal(MailContents.MailTemplateHtml, initialPayload.Template);

        string custom = MailContents.MailTemplateHtml.Replace("<title>Snap Hutao Mail Template</title>", "<title>Custom Mail Template</title>", StringComparison.Ordinal);
        try
        {
            TestResponse saved = await client.SendEnvelopeAsync("PUT", "/api/admin/mail/template", JsonSerializer.Serialize(new MailTemplateUpdateRequest { Template = custom }), maintainer.AccessToken);
            Assert.Equal(ReturnCodes.Success, saved.Retcode);

            TestResponse fetched = await client.SendEnvelopeAsync("GET", "/api/admin/mail/template", null, maintainer.AccessToken);
            MailTemplatePayload savedPayload = fetched.DeserializeData<MailTemplatePayload>()!;
            Assert.True(savedPayload.Customized);
            Assert.Equal(custom, savedPayload.Template);

            TestResponse rejected = await client.SendEnvelopeAsync("PUT", "/api/admin/mail/template", JsonSerializer.Serialize(new MailTemplateUpdateRequest { Template = "<html>missing placeholders</html>" }), maintainer.AccessToken);
            Assert.Equal(ReturnCodes.InvalidUploadData, rejected.Retcode);
        }
        finally
        {
            await client.SendEnvelopeAsync("DELETE", "/api/admin/mail/template", null, maintainer.AccessToken);
        }

        TestResponse restored = await client.SendEnvelopeAsync("GET", "/api/admin/mail/template", null, maintainer.AccessToken);
        Assert.False(restored.DeserializeData<MailTemplatePayload>()!.Customized);
    }
}
