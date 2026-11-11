using server.Models.Admin;
using server.Models.Mail;
using server.Services.Mail;
using server.Tests.Infrastructure;
using System.Net;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class MailContentTests(HutaoServerFactory factory)
{
    private const string RegisterCodeKind = "RegisterCode";
    private const string CancelRegistrationKind = "CancelRegistrationCode";
    private const string ChsLanguage = "CHS";
    private const string EnLanguage = "EN";

    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    private static MailContentUpdateRequest Content(string language, string suffix, string rawContent)
    {
        return new MailContentUpdateRequest
        {
            Language = language,
            Subject = $"Custom Subject {suffix}",
            Title = $"Custom Title {suffix}",
            RawContent = rawContent,
            Footer = $"Custom Footer {suffix}",
        };
    }

    private async Task<TestResponse> SendAsync(string method, string url, MailContentUpdateRequest? request, string token)
    {
        using HttpRequestMessage message = new(new HttpMethod(method), url);
        message.WithToken(token);
        message.WithDevice();
        message.WithClientVersion();
        if (request is not null)
        {
            message.Content = JsonContent.Create(request);
        }

        using HttpResponseMessage response = await client.SendAsync(message, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.StatusCode is not HttpStatusCode.InternalServerError, $"{method} {url} returned 500: {body}");

        return TestResponse.Parse(body);
    }

    private Task<TestResponse> GetListAsync(string token)
    {
        return SendAsync("GET", "/api/admin/mail/contents", null, token);
    }

    private Task<TestResponse> GetContentAsync(string kind, string language, string token)
    {
        return SendAsync("GET", $"/api/admin/mail/content/{kind}?language={language}", null, token);
    }

    private Task<TestResponse> PutContentAsync(string kind, MailContentUpdateRequest request, string token)
    {
        return SendAsync("PUT", $"/api/admin/mail/content/{kind}", request, token);
    }

    private Task<TestResponse> DeleteContentAsync(string kind, string language, string token)
    {
        return SendAsync("DELETE", $"/api/admin/mail/content/{kind}?language={language}", null, token);
    }

    private async Task<MailContentPayload> ReadContentAsync(string kind, string language, string token)
    {
        TestResponse parsed = await GetContentAsync(kind, language, token);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        MailContentPayload? payload = parsed.DeserializeData<MailContentPayload>();
        Assert.NotNull(payload);
        return payload!;
    }

    private static void AssertIsDefault(MailContentPayload payload, MailContent expected)
    {
        Assert.False(payload.Customized);
        Assert.Equal(expected.Subject, payload.Subject);
        Assert.Equal(expected.Title, payload.Title);
        Assert.Equal(expected.RawContent, payload.RawContent);
        Assert.Equal(expected.Footer, payload.Footer);
    }

    [Fact]
    public async Task List_ReturnsEveryCatalogKindInOrderWithOneFlagPerLanguage()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await GetListAsync(maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(ServerKeys.ServerAdminMailContentListSucceed, parsed.L10nKey);

        List<MailContentSummary>? summaries = parsed.DeserializeData<List<MailContentSummary>>();
        Assert.NotNull(summaries);
        Assert.Equal(MailContents.Kinds.Select(kind => kind.Key), summaries!.Select(item => item.Kind));

        foreach (MailContentSummary item in summaries)
        {
            MailContentKind definition = MailContents.Get(item.Kind);
            Assert.Equal(definition.Variables, item.Variables);
            Assert.Equal(MailContents.Languages.Length, item.Customized.Length);
        }
    }

    [Fact]
    public async Task Get_WithoutStoredContent_ReturnsTheBuiltInDefault()
    {
        TestUser maintainer = await users.MaintainerAsync();

        // 先清掉可能残留的自定义文案，保证初始状态是内置默认
        await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);

        TestResponse parsed = await GetContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.Success, parsed.Retcode);
        Assert.Equal(ServerKeys.ServerAdminMailContentGetSucceed, parsed.L10nKey);

        MailContentPayload payload = parsed.DeserializeData<MailContentPayload>()!;
        Assert.Equal(RegisterCodeKind, payload.Kind);
        Assert.Equal(ChsLanguage, payload.Language);
        Assert.Equal(MailContents.Get(RegisterCodeKind).Variables, payload.Variables);
        AssertIsDefault(payload, MailContents.Get(RegisterCodeKind).Chs);
    }

    [Fact]
    public async Task Put_ThenGet_ReturnsTheSavedContentAndMarksItCustomized()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string suffix = Guid.NewGuid().ToString("N");
        MailContentUpdateRequest request = Content(ChsLanguage, suffix, $"<p>Custom body {suffix} {{{{code}}}}</p>");

        await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
        try
        {
            TestResponse saved = await PutContentAsync(RegisterCodeKind, request, maintainer.AccessToken);

            Assert.Equal(ReturnCodes.Success, saved.Retcode);
            Assert.Equal(ServerKeys.ServerAdminMailContentSaveSucceed, saved.L10nKey);

            MailContentPayload payload = await ReadContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
            Assert.True(payload.Customized);
            Assert.Equal(request.Subject, payload.Subject);
            Assert.Equal(request.Title, payload.Title);
            Assert.Equal(request.RawContent, payload.RawContent);
            Assert.Equal(request.Footer, payload.Footer);
        }
        finally
        {
            await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
        }
    }

    [Fact]
    public async Task Put_RawContentWithoutRequiredVariable_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);

        TestResponse rejected = await PutContentAsync(RegisterCodeKind, Content(ChsLanguage, Guid.NewGuid().ToString("N"), "<p>no runtime variable here</p>"), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, rejected.Retcode);
        Assert.Equal(ServerKeys.ServerAdminMailContentVariableRequired, rejected.L10nKey);

        // 被拒的请求不能留下任何自定义内容
        MailContentPayload payload = await ReadContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
        Assert.False(payload.Customized);
    }

    [Fact]
    public async Task Put_WithUnknownKindUnsupportedLanguageOrBlankField_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        string suffix = Guid.NewGuid().ToString("N");

        await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);

        TestResponse unknownKind = await PutContentAsync("NotAMailKind", Content(ChsLanguage, suffix, "<p>{{code}}</p>"), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, unknownKind.Retcode);
        Assert.Equal(ServerKeys.ServerAdminMailContentKindInvalid, unknownKind.L10nKey);

        TestResponse unknownLanguage = await PutContentAsync(RegisterCodeKind, Content("JPY", suffix, "<p>{{code}}</p>"), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, unknownLanguage.Retcode);
        Assert.Equal(ServerKeys.ServerAdminMailContentLanguageInvalid, unknownLanguage.L10nKey);

        MailContentUpdateRequest blankSubject = Content(ChsLanguage, suffix, "<p>{{code}}</p>");
        blankSubject.Subject = "   ";
        TestResponse emptyField = await PutContentAsync(RegisterCodeKind, blankSubject, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.InvalidUploadData, emptyField.Retcode);
        Assert.Equal(ServerKeys.ServerAdminMailContentEmpty, emptyField.L10nKey);

        MailContentPayload payload = await ReadContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
        Assert.False(payload.Customized);
    }

    [Fact]
    public async Task Delete_RestoresTheDefaultAndASecondDeleteReportsNotFound()
    {
        TestUser maintainer = await users.MaintainerAsync();
        MailContentUpdateRequest request = Content(ChsLanguage, Guid.NewGuid().ToString("N"), "<p>{{code}}</p>");

        try
        {
            Assert.Equal(ReturnCodes.Success, (await PutContentAsync(RegisterCodeKind, request, maintainer.AccessToken)).Retcode);

            TestResponse deleted = await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);

            Assert.Equal(ReturnCodes.Success, deleted.Retcode);
            Assert.Equal(ServerKeys.ServerAdminMailContentResetSucceed, deleted.L10nKey);

            MailContentPayload restored = await ReadContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
            AssertIsDefault(restored, MailContents.Get(RegisterCodeKind).Chs);

            TestResponse again = await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
            Assert.Equal(ReturnCodes.NotFound, again.Retcode);
            Assert.Equal(ServerKeys.ServerAdminMailContentNotFound, again.L10nKey);
        }
        finally
        {
            await DeleteContentAsync(RegisterCodeKind, ChsLanguage, maintainer.AccessToken);
        }
    }

    [Fact]
    public async Task Put_OnlyCustomizesTheRequestedLanguage()
    {
        TestUser maintainer = await users.MaintainerAsync();

        // 两种语言都回到默认，避免其它用例留下的自定义内容干扰断言
        await DeleteContentAsync(CancelRegistrationKind, ChsLanguage, maintainer.AccessToken);
        await DeleteContentAsync(CancelRegistrationKind, EnLanguage, maintainer.AccessToken);

        MailContent englishDefault = MailContents.Get(CancelRegistrationKind).En;
        try
        {
            MailContentPayload before = await ReadContentAsync(CancelRegistrationKind, EnLanguage, maintainer.AccessToken);
            AssertIsDefault(before, englishDefault);

            MailContentUpdateRequest request = Content(ChsLanguage, Guid.NewGuid().ToString("N"), "<p>{{code}}</p>");
            Assert.Equal(ReturnCodes.Success, (await PutContentAsync(CancelRegistrationKind, request, maintainer.AccessToken)).Retcode);

            MailContentPayload chinese = await ReadContentAsync(CancelRegistrationKind, ChsLanguage, maintainer.AccessToken);
            Assert.True(chinese.Customized);
            Assert.Equal(request.Subject, chinese.Subject);

            // 同一种类的英文文案不受影响
            MailContentPayload english = await ReadContentAsync(CancelRegistrationKind, EnLanguage, maintainer.AccessToken);
            AssertIsDefault(english, englishDefault);

            // 列表接口的 Customized 按 Languages 下标区分语言
            TestResponse listed = await GetListAsync(maintainer.AccessToken);
            List<MailContentSummary>? summaries = listed.DeserializeData<List<MailContentSummary>>();
            Assert.NotNull(summaries);
            MailContentSummary summary = summaries!.Single(item => item.Kind is CancelRegistrationKind);
            Assert.True(summary.Customized[0]);
            Assert.False(summary.Customized[1]);
        }
        finally
        {
            await DeleteContentAsync(CancelRegistrationKind, ChsLanguage, maintainer.AccessToken);
            await DeleteContentAsync(CancelRegistrationKind, EnLanguage, maintainer.AccessToken);
        }
    }
}
