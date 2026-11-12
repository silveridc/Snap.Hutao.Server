using server.Models.Admin;
using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class UserManagementTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task UserResetPassword_WithShortPassword_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-resetpwd");

        TestResponse parsed = await client.SendEnvelopeAsync("PUT", $"/api/admin/users/{target.Id}/password", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = target.Id,
            NewPassword = "short",
        }), maintainer.AccessToken);

        Assert.Equal(ReturnCodes.InvalidUploadData, parsed.Retcode);
    }

    [Fact]
    public async Task UserResetPassword_ChangesThePasswordOfTheTargetAccount()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-resetpwd-ok");
        const string newPassword = "AdminIssuedPassword1";

        TestResponse parsed = await client.SendEnvelopeAsync("PUT", $"/api/admin/users/{target.Id}/password", JsonSerializer.Serialize(new UserResetPasswordRequest
        {
            UserId = target.Id,
            NewPassword = newPassword,
        }), maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        using HttpResponseMessage response = await client.PostJsonWithTokenAsync(
            "/api/passport/v2/sessions",
            new Dictionary<string, object?>
            {
                ["UserName"] = RsaPayload.Encrypt(target.UserName),
                ["Password"] = RsaPayload.Encrypt(newPassword),
            },
            token: null);

        Assert.Equal(ReturnCodes.Success, (await response.ReadTestResponseAsync()).Retcode);
    }

    [Fact]
    public async Task UserList_FiltersByKeywordAndPaging()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser target = await users.CreateUniqueAsync("service-list-keyword");

        TestResponse parsed = await client.SendEnvelopeAsync(
            "GET", $"/api/admin/users?page=1&pageSize=10&keyword={Uri.EscapeDataString(target.UserName)}", null, maintainer.AccessToken);

        PagedResult<UserListItem>? page = parsed.DeserializeData<PagedResult<UserListItem>>();
        Assert.NotNull(page);
        Assert.Contains(page!.Items, item => item.Id == target.Id);
    }

    [Fact]
    public async Task UserStatistics_CountsAtLeastTheSeededAccounts()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("GET", "/api/admin/users/statistics", null, maintainer.AccessToken);

        UserStatistics? statistics = parsed.DeserializeData<UserStatistics>();
        Assert.NotNull(statistics);
        Assert.True(statistics!.TotalUsers > 0);
        Assert.True(statistics.Maintainers >= 1);
    }

    [Fact]
    public async Task LicenseApprove_WithIncompleteParameters_IsRejected()
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("POST", "/api/admin/license-applications/approval?userName=&code=", null, maintainer.AccessToken);

        Assert.Equal(ReturnCodes.LicenseApprovalFailed, parsed.Retcode);
    }

    [Fact]
    public async Task LicenseApprove_ForAnExistingApplication_GrantsDeveloperLicense()
    {
        TestUser maintainer = await users.MaintainerAsync();
        TestUser applicant = await users.CreateUniqueAsync("service-license");
        const string approvalCode = "INTEGRATIONAPPROVALCODE00000001";

        await factory.WithDbContextAsync(async db =>
        {
            db.Licenses.Add(new server.Models.Entity.LicenseApplicationRecord
            {
                UserId = applicant.Id,
                ProjectUrl = "https://example.com/project",
                ApprovalCode = approvalCode,
            });
            await db.SaveChangesAsync();
        });

        TestResponse parsed = await client.SendEnvelopeAsync(
            "POST", $"/api/admin/license-applications/approval?userName={Uri.EscapeDataString(applicant.UserName)}&code={approvalCode}", null, maintainer.AccessToken);
        Assert.Equal(ReturnCodes.Success, parsed.Retcode);

        TestResponse detail = await client.SendEnvelopeAsync("GET", $"/api/admin/users/{applicant.Id}", null, maintainer.AccessToken);
        Assert.True(detail.DeserializeData<UserDetail>()!.IsLicensedDeveloper);
    }
}
