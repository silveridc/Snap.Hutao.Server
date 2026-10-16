using Garnet;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using server.Models.Context;
using server.Options;
using System.Net;

namespace server.Tests.Infrastructure;

/// <summary>
/// 进程内托管整个 Web API 的测试宿主：
/// <list type="bullet">
/// <item>用 Garnet 提供 Redis 兼容服务（应用启动即要求 Redis 可达）；</item>
/// <item>把两个 DbContext 指向独立的 MySQL 测试库（生产同样使用 MySQL，语义一致）；</item>
/// <item>用假 SMTP 服务截获验证码邮件；</item>
/// <item>关闭 Unix Socket、RealIp 与 Sentry/OpenTelemetry 上报。</item>
/// </list>
/// </summary>
public sealed class HutaoServerFactory : WebApplicationFactory<server.Models.Response.Response>, IAsyncLifetime
{
    private readonly string stateDirectory = Path.Combine(Path.GetTempPath(), $"hutao-tests-{Guid.NewGuid():N}");
    private readonly StubHttpMessageHandler stubHttp = new();
    private GarnetServer? garnet;
    private FakeSmtpServer? smtp;
    private readonly Lazy<TestUsers> users;

    public HutaoServerFactory()
    {
        users = new Lazy<TestUsers>(() => new TestUsers(this));
    }

    public int RedisPort { get; private set; }

    /// <summary>
    /// 进程内共享的测试账号工厂。xUnit 会为每个测试方法新建测试类实例，
    /// 把账号缓存在宿主上可以让「维护者」等固定账号只播种一次。
    /// </summary>
    public TestUsers Users => users.Value;

    /// <summary>测试使用的假 SMTP 服务。</summary>
    public FakeSmtpServer Smtp => smtp ?? throw new InvalidOperationException("Factory is not initialized.");

    /// <summary>对外部 HTTP 调用的拦截器。</summary>
    public StubHttpMessageHandler StubHttp => stubHttp;

    /// <summary>通过被测服务容器的 DI 解析一个服务。</summary>
    public T GetService<T>()
        where T : notnull
    {
        return Services.GetRequiredService<T>();
    }

    /// <summary>在独立作用域中解析一个服务。</summary>
    public T GetScopedService<T>()
        where T : notnull
    {
        using IServiceScope scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(stateDirectory);

        RedisPort = TestEnvironment.FindFreePort();
        garnet = new GarnetServer([
            "--port", RedisPort.ToString(),
            "--bind", "127.0.0.1",
            "--checkpointdir", Path.Combine(stateDirectory, "garnet"),
        ]);
        garnet.Start();

        smtp = new FakeSmtpServer(TestEnvironment.SmtpPort);
        RegisterDefaultHttpStubs();

        await ResetDatabaseAsync();
        await PrepareSchemaAsync();
    }

    /// <summary>
    /// 登记第三方接口的默认桩数据，保证接口在无外网环境下也能返回成功分支。
    /// 需要断言失败分支的用例可自行 <see cref="StubHttpMessageHandler.Map"/> 覆盖（后登记优先）。
    /// </summary>
    private void RegisterDefaultHttpStubs()
    {
        // Enka 展柜
        stubHttp.Map(
            request => request.RequestUri?.AbsoluteUri.StartsWith("https://enka.network/api/uid/", StringComparison.OrdinalIgnoreCase) is true,
            request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"playerInfo":{"nickname":"TestPlayer","level":60,"signature":"","worldLevel":8,"nameCardId":210001,"finishAchievementNum":1,"towerFloorIndex":12,"towerLevelIndex":3,"showAvatarInfoList":[],"showNameCardIdList":[]},"avatarInfoList":[],"ttl":60}""",
                    Encoding.UTF8,
                    "application/json"),
            });

        // Bing 每日壁纸
        stubHttp.MapJson(
            "HPImageArchive.aspx",
            """{"images":[{"enddate":"20260101","urlbase":"/th?id=OHR.Test","copyrightlink":"https://www.bing.com/test","copyright":"Test Copyright","title":"Test"}]}""");

        // HoYoPlay 启动器壁纸
        stubHttp.MapJson(
            "hyp-api.mihoyo.com",
            """{"retcode":0,"message":"OK","data":{"game_info_list":[{"game":{"biz":"hk4e_cn"},"backgrounds":[{"background":{"url":"https://example.com/wallpaper.jpg"}}]}]}}""");

        // reCAPTCHA 校验：默认判定为不通过，避免误发许可证
        stubHttp.MapJson(
            "recaptcha/api/siteverify",
            """{"success":false,"score":0.0,"action":""}""");

        // 爱发电订单查询：默认返回空列表（订单校验不通过），
        // 否则 Webhook 会按失败重试策略原地等待 10 次 3 秒
        stubHttp.MapJson(
            "afdian.com/api/open/query-order",
            """{"ec":200,"em":"","data":{"list":[]}}""");

        // GitHub OAuth：默认返回失败，用例可通过覆盖此规则模拟成功
        stubHttp.MapJson("github.com/login/oauth/access_token", """{"error":"stubbed"}""", HttpStatusCode.BadRequest);
        stubHttp.MapJson("api.github.com/user", """{"error":"stubbed"}""", HttpStatusCode.Unauthorized);

        // CDN 调度服务：返回一个可用的下载令牌与加速镜像
        stubHttp.MapJson("hutao-cdn.test/v2/getDownloadToken", """{"retcode":0,"message":"ok","data":"stub-download-token"}""");
        stubHttp.MapJson("hutao-cdn.test/get", """{"retcode":0,"message":"ok","data":{"mirrorName":"stub","url":"https://hutao-cdn.test/files/hutao.zip"}}""");
    }

    /// <summary>应用 AppDbContext 的迁移，并建出无迁移的 MetadataDbContext 表。</summary>
    private async Task PrepareSchemaAsync()
    {
        using IServiceScope scope = Services.CreateScope();

        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await appDbContext.Database.MigrateAsync();

        MetadataDbContext metadataDbContext = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        // MetadataDbContext 没有迁移，且库已存在，EnsureCreated 不会建表，直接建表
        await metadataDbContext.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        smtp?.Dispose();
        garnet?.Dispose();

        try
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Garnet 的检查点文件可能仍被占用，清理失败不影响测试结果
        }
    }

    /// <summary>删除并重建测试库，使每个测试进程从干净的 schema 开始。</summary>
    public async Task ResetDatabaseAsync()
    {
        (string user, string password) = TestEnvironment.AppCredentials;

        await using (MySqlConnection connection = new(TestEnvironment.AdminConnectionString))
        {
            await connection.OpenAsync();

            foreach (string sql in new[]
            {
                $"DROP DATABASE IF EXISTS `{TestEnvironment.DatabaseName}`;",
                $"CREATE DATABASE `{TestEnvironment.DatabaseName}` CHARACTER SET utf8mb4;",
                $"CREATE USER IF NOT EXISTS '{user}'@'localhost' IDENTIFIED BY '{password}';",
                $"GRANT ALL PRIVILEGES ON `{TestEnvironment.DatabaseName}`.* TO '{user}'@'localhost';",
                $"GRANT ALL PRIVILEGES ON `{TestEnvironment.DatabaseName}`.* TO 'snaphutao_server'@'localhost';",
                "FLUSH PRIVILEGES;",
            })
            {
                await using MySqlCommand command = new(sql, connection);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    /// <summary>创建不跟随重定向的客户端，便于断言 3xx 响应（OAuth 授权等）。</summary>
    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        string connectionString = TestEnvironment.AppConnectionString;
        string rsaKeyPath = Path.Combine(AppContext.BaseDirectory, "privkey.key");

        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:PrimaryMysql8", connectionString);
        builder.UseSetting("ConnectionStrings:MetadataMysql8", connectionString);
        builder.UseSetting("App:RedisAddress", $"127.0.0.1:{RedisPort}");
        builder.UseSetting("App:Socket:Status", "false");
        builder.UseSetting("App:RealIp:Status", "false");
        builder.UseSetting("App:RSAPrivateKey", rsaKeyPath);
        builder.UseSetting("App:Smtp:Server", "127.0.0.1");
        builder.UseSetting("App:Smtp:UserName", "noreply@example.com");
        builder.UseSetting("App:Smtp:Password", "test-password");
        builder.UseSetting("App:Smtp:DiagnosticEmailAddress", "admin@example.com");

        builder.UseSetting("App:Github:ClientId", "test-client-id");
        builder.UseSetting("App:Github:ClientSecret", "test-client-secret");

        // appsettings.json 里 CDN 调度服务地址为空，相对地址会让 HttpClient 直接抛异常；
        // 给一个专用域名，配合下面的桩覆盖 /Distribution/* 的成功分支
        builder.UseSetting("App:CdnEndpoint", "https://hutao-cdn.test");
        builder.UseSetting("App:CdnToken", "test-cdn-token");

        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

        builder.ConfigureTestServices(services =>
        {
            DisableSentry(services);
            DisableOpenTelemetry(services);
            DisableQuartz(services);
            OverrideAppOptions(services);
            OverrideHttpClients(services);

            // TestServer 不设置 RemoteIpAddress，/api/v1/ip 会因此空引用；这里补上回环地址模拟真实请求
            services.AddSingleton<IStartupFilter, LoopbackRemoteIpStartupFilter>();
        });
    }

    /// <summary>
    /// Program.cs 里所有 Quartz 任务都是 <c>StartNow()</c> 调度的，宿主一启动就会并发跑统计刷新和
    /// 记录清理，测试数据的读写时机无法预测。统计接口由用例显式触发，这里去掉调度器的后台服务。
    /// </summary>
    private static void DisableQuartz(IServiceCollection services)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            ServiceDescriptor descriptor = services[i];
            string? typeName = descriptor.ImplementationType?.FullName ?? descriptor.ImplementationFactory?.GetType().FullName;
            if (descriptor.ServiceType == typeof(IHostedService) && typeName?.Contains("Quartz", StringComparison.Ordinal) is true)
            {
                services.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 所有出网 HttpClient 都改走 <see cref="StubHttpMessageHandler"/>：
    /// 未登记的地址一律返回 502，保证测试不依赖外网。
    /// <para>
    /// <c>AddHttpClient</c> 不会把 <see cref="HttpMessageHandler"/> 注册进容器，
    /// 主处理器由 <see cref="IHttpMessageHandlerBuilderFilter"/> 在构建管道时创建，
    /// 因此这里插入一个过滤器替换主处理器。
    /// </para>
    /// </summary>
    private void OverrideHttpClients(IServiceCollection services)
    {
        services.AddSingleton<IHttpMessageHandlerBuilderFilter>(new StubHandlerBuilderFilter(stubHttp));
    }

    private sealed class StubHandlerBuilderFilter(StubHttpMessageHandler handler) : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        {
            return builder =>
            {
                next(builder);

                // Sentry 与 OpenTelemetry 的上报管道必须保持原样，避免测试流量被吞掉
                string? name = builder.Name;
                if (name is not null &&
                    (name.Contains("Sentry", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("OpenTelemetry", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                builder.PrimaryHandler = handler;
            };
        }
    }

    /// <summary>
    /// appsettings.json 缺少 OAuthStateEncryptKey（线上由环境变量注入），
    /// 这里替换掉 Program.cs 注册的 AppOptions 单例，补上 32 字节的 AES 密钥。
    /// </summary>
    private static void OverrideAppOptions(IServiceCollection services)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(AppOptions))
            {
                services.RemoveAt(i);
            }
        }

        services.AddSingleton(serviceProvider =>
        {
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            AppOptions options = configuration.GetSection("App").Get<AppOptions>()!;
            options.OAuthStateEncryptKey = new byte[32];
            for (int i = 0; i < options.OAuthStateEncryptKey.Length; i++)
            {
                options.OAuthStateEncryptKey[i] = (byte)(i * 7 + 1);
            }

            return options;
        });
    }

    private static void DisableSentry(IServiceCollection services)
    {
        // UseSentry 通过 options 配置注册，后注册的配置会覆盖前一个，置空 Dsn 即可完全关闭上报
        services.Configure<SentryOptions>(options =>
        {
            options.Dsn = null;
            options.Debug = false;
            options.TracesSampleRate = null;
        });
    }

    private static void DisableOpenTelemetry(IServiceCollection services)
    {
        for (int i = services.Count - 1; i >= 0; i--)
        {
            ServiceDescriptor descriptor = services[i];
            string? typeName = descriptor.ImplementationType?.FullName ?? descriptor.ImplementationFactory?.GetType().FullName;
            if (descriptor.ServiceType == typeof(IHostedService) && typeName?.Contains("OpenTelemetry", StringComparison.Ordinal) is true)
            {
                services.RemoveAt(i);
            }
        }
    }

    /// <summary>在独立作用域中执行一段数据库操作。</summary>
    public async Task WithDbContextAsync(Func<AppDbContext, Task> action)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(appDbContext);
    }

    /// <summary>在独立作用域中读取数据。</summary>
    public async Task<T> WithDbContextAsync<T>(Func<AppDbContext, Task<T>> selector)
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await selector(appDbContext);
    }

    private sealed class LoopbackRemoteIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                    await nextMiddleware();
                });

                next(app);
            };
        }
    }
}
