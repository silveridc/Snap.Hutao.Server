using server.Tests.Infrastructure;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class ServiceAuthTests(HutaoServerFactory factory)
{
    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    public static TheoryData<string, string, string?> AllEndpoints => ServiceEndpoints.TheoryData;

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task WithoutToken_IsRejectedWithLoginFail(string method, string url, string? json)
    {
        TestResponse parsed = await client.SendEnvelopeAsync(method, url, json, token: null);

        // [Authorize] 失败时由 ResponseAuthorizationMiddlewareResultHandler 转成统一外壳，HTTP 状态码仍是 200
        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task WithNonMaintainer_IsRejectedWithServiceKeyInvalid(string method, string url, string? json)
    {
        TestUser plain = await users.PlainAsync();

        TestResponse parsed = await client.SendEnvelopeAsync(method, url, json, plain.AccessToken);

        Assert.Equal(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
        Assert.Equal("Only official staff can perform this action", parsed.Message);
    }

    [Theory]
    [MemberData(nameof(AllEndpoints))]
    public async Task WithMaintainer_ReachesTheBusinessLogic(string method, string url, string? json)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync(method, url, json, maintainer.AccessToken);

        // 维护者身份必须能穿过权限过滤器：返回码应落在业务分支上，而不是鉴权失败
        Assert.NotEqual(ReturnCodes.ServiceKeyInvalid, parsed.Retcode);
        Assert.NotEqual(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Fact]
    public async Task MaintainerEndpoints_ReturnTheDocumentedRetcode()
    {
        TestUser maintainer = await users.MaintainerAsync();

        foreach (ServiceEndpoint endpoint in ServiceEndpoints.Rows)
        {
            TestResponse parsed = await client.SendEnvelopeAsync(endpoint.Method, endpoint.Url, endpoint.Json, maintainer.AccessToken);

            // 有状态的接口不带固定期望值，业务分支由各自的用例断言
            if (endpoint.MaintainerRetcode is int expected)
            {
                Assert.True(expected == parsed.Retcode,
                    $"{endpoint.Method} {endpoint.Url} expected retcode {expected} but got {parsed.Retcode} ({parsed.Message})");
            }
        }
    }
}
