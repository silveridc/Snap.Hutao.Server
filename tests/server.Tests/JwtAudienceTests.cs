using Microsoft.IdentityModel.Tokens;
using server.Options;
using server.Tests.Infrastructure;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace server.Tests;

[Collection(HutaoServerCollection.Name)]
public sealed class JwtAudienceTests(HutaoServerFactory factory)
{
    private const string AdminEndpoint = "/api/admin/job-logs?page=1&pageSize=1";

    private readonly HttpClient client = factory.CreateApiClient();
    private readonly TestUsers users = factory.Users;

    [Fact]
    public async Task AccessToken_CarriesTheConfiguredAudience()
    {
        TestUser maintainer = await users.MaintainerAsync();

        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(maintainer.AccessToken);

        Assert.Equal(factory.GetService<AppOptions>().Jwt.Audience, Assert.Single(token.Audiences));
    }

    [Theory]
    [InlineData(null)]                 // 没有 aud 声明（旧格式令牌 / 不带受众的伪造令牌）
    [InlineData("some-other-service")] // 受众与本服务不符
    public async Task TokenWithWrongAudience_IsRejectedAsLoginFail(string? audience)
    {
        TestUser maintainer = await users.MaintainerAsync();

        TestResponse parsed = await client.SendEnvelopeAsync("GET", AdminEndpoint, null, ForgeToken(maintainer, audience));

        Assert.Equal(ReturnCodes.LoginFail, parsed.Retcode);
    }

    [Fact]
    public async Task TokenWithTheCorrectAudience_PassesAuthentication()
    {
        TestUser maintainer = await users.MaintainerAsync();
        AppOptions options = factory.GetService<AppOptions>();

        TestResponse parsed = await client.SendEnvelopeAsync("GET", AdminEndpoint, null, ForgeToken(maintainer, options.Jwt.Audience));

        // 鉴权层放行即可：业务分支的返回值与本用例无关
        Assert.NotEqual(ReturnCodes.LoginFail, parsed.Retcode);
    }

    /// <summary>
    /// 用服务端真实的签发参数伪造一枚令牌，只改变受众，以验证校验端确实在比对 aud.
    /// </summary>
    private string ForgeToken(TestUser user, string? audience)
    {
        AppOptions options = factory.GetService<AppOptions>();
        SecurityTokenDescriptor descriptor = new()
        {
            Subject = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString())]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            Issuer = options.Jwt.Issuer,
            Audience = audience,
            SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Jwt.Raw)), SecurityAlgorithms.HmacSha256Signature),
        };

        JwtSecurityTokenHandler handler = new();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}
