using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using server.Filter;
using server.Services.Admin;
using server.Services.Cache;
using server.Services.Mail;
using server.Services.WallPaper;
using server.Services.Yae;
using StackExchange.Redis;

namespace server;

[SuppressMessage("", "SA1401")]
public static class Program
{
    public const string applicationVersion = "server@v1.0.5";
    public static string AppResourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
    private static ILogger? _logger;

    public static async Task Main(string[] args)
    {
        WebApplicationBuilder appBuilder = WebApplication.CreateBuilder(args);
        AppOptions appOptions = appBuilder.Configuration.GetSection("App").Get<AppOptions>()!;
        IServiceCollection services = appBuilder.Services;

        if (appOptions.Socket.Status)
        {
            appBuilder.WebHost.ConfigureKestrel(options =>
            {
                string socketPath = appOptions.Socket.Path;
                if (File.Exists(socketPath))
                {
                    try
                    {
                        File.Delete(socketPath);
                    }
                    catch (IOException ex)
                    {
                        Console.WriteLine("Remove Socket File Fail" + ex);
                    }
                }

                options.ListenUnixSocket(socketPath);
            });
        }

        appBuilder.WebHost.UseSentry(options =>
        {
            options.Release = applicationVersion;
            options.Dsn = appOptions.SentryDsn;
            options.TracesSampleRate = 1D;
            options.SendDefaultPii = true;

            options.AutoSessionTracking = true;
        });
        services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("snap-hutao-server", serviceVersion: applicationVersion))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(appOptions.OtlpTracesEndpoint);
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                    otlp.HttpClientFactory = () =>
                    {
                        HttpClient client = new();
                        client.DefaultRequestHeaders.Add("x-sentry-auth", appOptions.OtlpToken);
                        return client;
                    };
                }));

        appBuilder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("snap-hutao-server", serviceVersion: applicationVersion));
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = true;
            logging.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(appOptions.OtlpLogsEndpoint);
                otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                otlp.HttpClientFactory = () =>
                {
                    HttpClient client = new();
                    client.DefaultRequestHeaders.Add("x-sentry-auth", appOptions.OtlpToken);
                    return client;
                };
            });
        });

        if (appOptions.RealIp?.Status is true)
        {
            appBuilder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
                options.ForwardedForHeaderName = appOptions.RealIp.Header;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                options.ForwardLimit = null;
            });
        }

        services.AddSentryTunneling();
        string assetsPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Assets");
        string? redisConnectString = appBuilder.Configuration.GetConnectionString("DefaultRedisConnection");
        if (!string.IsNullOrEmpty(redisConnectString))
        {
            Lazy<IConnectionMultiplexer> redisMultiplexer = new(() => ConnectionMultiplexer.Connect(redisConnectString));
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult(redisMultiplexer.Value);
                options.InstanceName = DistributedCacheExtension.InstanceName;
            })
            .AddSingleton<IConnectionMultiplexer>(_ => redisMultiplexer.Value);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        // Services
        services
            .AddAuthorization()
            .AddCors(options => options.AddDefaultPolicy(builder => builder.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin()))
            .AddDbContextPool<AppDbContext>((serviceProvider, options) =>
            {
                ILogger<AppDbContext> logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();
                options
                    .UseMySql(appBuilder.Configuration.GetConnectionString("DefaultMysqlConnection")!, new MySqlServerVersion(new Version(8, 4, 11)))
                    .ConfigureWarnings(action => action.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors();
            })
            .AddEndpointsApiExplorer()
            .AddHttpClient()
            .AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["retcode"] = context.ProblemDetails.Status ?? (int)ReturnCode.InternalStateException;
                context.ProblemDetails.Extensions["message"] = context.Exception?.Message ?? string.Empty;
            })
            .AddQuartz(config =>
            {
                config.ScheduleJob<GachaLogStatisticsRefreshJob>(trigger => trigger.StartNow().WithCronSchedule("0 30 */1 * * ?"));
                config.ScheduleJob<LegacyStatisticsRefreshJob>(trigger => trigger.StartNow().WithCronSchedule("0 5 */1 * * ?"));
                config.ScheduleJob<SpiralAbyssRecordCleanJob>(trigger => trigger.StartNow().WithCronSchedule("0 0 4 16 * ?"));
                config.ScheduleJob<RoleCombatStatisticsRefreshJob>(trigger => trigger.StartNow().WithCronSchedule("0 10 */1 * * ?"));
                config.ScheduleJob<RoleCombatRecordCleanJob>(trigger => trigger.StartNow().WithCronSchedule("0 0 4 1 * ?"));
                config.ScheduleJob<JobLogCleanJob>(trigger => trigger.StartNow().WithCronSchedule("0 0 5 * * ?"));
            })
            .AddQuartzHostedService(options => options.WaitForJobsToComplete = true)

            // .AddResponseCompression()
            .AddScoped<AccessionService>()
            .AddScoped<AnnouncementService>()
            .AddScoped<DistributionService>()
            .AddScoped<GachaLogService>()
            .AddScoped<GithubService>()
            .AddKeyedScoped<IOAuthProvider, GithubService>(OAuthProviderKind.Github)
            .AddScoped<PassportService>()
            .AddScoped<PassportVerificationService>()
            .AddScoped<UserLoginLogService>()
            .AddScoped<PizzaHelperRecordService>()
            .AddScoped<RecordService>()
            .AddScoped<SpiralAbyssStatisticsService>()
            .AddScoped<TelemetryService>()
            .AddScoped<ClientVersionService>()
            .AddScoped<RedeemService>()
            .AddScoped<UserAdminService>()
            .AddScoped<BanService>()
            .AddScoped<AdminQueryService>()
            .AddScoped<AdminSsoService>()
            .AddScoped<SystemOverviewService>()
            .AddScoped<AnalyticsService>()
            .AddSingleton<OidcMetadata>()
            .AddScoped<ReleaseService>()
            .AddScoped<ReleaseBucketService>()
            .AddScoped<BingWallpaper>()
            .AddScoped<HoyoPlayWallpaper>()
            .AddScoped<CuratedWallpaper>()
            .AddScoped<GithubApiService>()
            .AddScoped<StatisticsService>()
            .AddScoped<ReCaptchaService>()
            .AddScoped<RoleCombatService>()
            .AddScoped<AuditLogActionFilter>()
            .AddSingleton<ClientVersionTrackFilter>()
            .AddScoped<ValidateCdnPermission>()
            .AddScoped<ValidateGachaLogPermission>()
            .AddScoped<ValidateMaintainPermission>()
            .AddScoped<ValidateAdminWritePermission>()
            .AddScoped<GachaLogStatisticsService>()
            .AddScoped<YaeMetadataService>()
            .AddScoped<AfdianWebhookService>()

            .AddSingleton<CacheService>()
            .AddSingleton<CdnExpireService>()
            .AddSingleton<GachaLogExpireService>()
            .AddSingleton<SystemConfigService>()
            .AddSingleton<IAuthorizationMiddlewareResultHandler, ResponseAuthorizationMiddlewareResultHandler>()
            .AddSingleton<IRankService, RankService>()
            .AddSingleton<MailService>()
            .AddSingleton<ISentryUserFactory, SentryUserFactory>()
            .AddKeyedSingleton<ISearcher>("IPv4DB", (sp, key) => new Searcher(CachePolicy.Content, Path.Combine(assetsPath, "ipv4.xdb")))
            .AddKeyedSingleton<ISearcher>("IPv6DB", (sp, key) => new Searcher(CachePolicy.Content, Path.Combine(assetsPath, "ipv6.xdb")))
            .AddSingleton<Services.PublicAPI.IpRegionService>()
            .AddSingleton(appOptions)

            // Quartz.
            .AddSingleton<JobLogListener>()
            .AddTransient<GachaLogStatisticsRefreshJob>()
            .AddTransient<LegacyStatisticsRefreshJob>()
            .AddTransient<RoleCombatRecordCleanJob>()
            .AddTransient<JobLogCleanJob>()
            .AddTransient<RoleCombatStatisticsRefreshJob>()
            .AddTransient<SpiralAbyssRecordCleanJob>();

        // Authentication
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new()
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = appOptions.GetJwtSecurityKey(),
                    ValidateIssuer = true,
                    ValidIssuer = appOptions.Jwt.Issuer,
                    // 令牌由 PassportService / AdminSsoService 签发，两者都写入了 aud；
                    // 校验端必须配同一个受众，并锁定 HS256，防止算法混淆
                    ValidateAudience = true,
                    ValidAudience = appOptions.Jwt.Audience,
                    RequireAudience = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(10),
                };
            });
        // Identity
        services
            .AddIdentityCore<HutaoUser>(options =>
            {
                PasswordOptions pwdOptions = options.Password;
                // 密码的最小长度要求
                pwdOptions.RequiredLength = appOptions.Password.RequiredLength;
                // 密码中至少要出现多少个互不相同的字符。设为 0 或 1 表示不额外限制重复。
                pwdOptions.RequiredUniqueChars = appOptions.Password.RequiredUniqueChars;
                // 是否必须包含数字
                pwdOptions.RequireDigit = appOptions.Password.RequireDigit;
                // 是否必须包含小写英文字母
                pwdOptions.RequireLowercase = appOptions.Password.RequireLowercase;
                // 是否必须包含大写英文字母
                pwdOptions.RequireUppercase = appOptions.Password.RequireUppercase;
                // 是否必须包含非字母数字的特殊符号
                pwdOptions.RequireNonAlphanumeric = appOptions.Password.RequireNonAlphanumeric;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
        // Swagger
        services
            .AddEndpointsApiExplorer()
            .AddSwaggerGen(options =>
            {
                options.SwaggerDoc("Public", new()
                {
                    Version = "1.0",
                    Title = "公共api",
                    Description = "公共api",
                });
                options.SwaggerDoc("SpiralAbyss", new()
                {
                    Version = "1.0",
                    Title = "深渊统计",
                    Description = "深渊统计数据",
                });
                options.SwaggerDoc("RoleCombat", new()
                {
                    Version = "1.0",
                    Title = "剧诗统计",
                    Description = "幻想真境剧诗",
                });
                options.SwaggerDoc("Passport", new()
                {
                    Version = "1.0",
                    Title = "账户",
                    Description = "账户",
                });
                options.SwaggerDoc("GachaLog", new()
                {
                    Version = "1.0",
                    Title = "祈愿记录",
                    Description = "账户祈愿记录管理",
                });
                options.SwaggerDoc("Distribution", new()
                {
                    Version = "1.0",
                    Title = "分发管理",
                    Description = "分发管理",
                });
                options.SwaggerDoc("Services", new()
                {
                    Version = "1.0",
                    Title = "服务管理",
                    Description = "维护专用管理接口，调用需要维护权限",
                });
                options.SwaggerDoc("Redeem", new()
                {
                    Version = "1.0",
                    Title = "兑换码",
                    Description = "兑换码相关接口",
                });
                options.SwaggerDoc("OAuth", new()
                {
                    Version = "1.0",
                    Title = "OAuth",
                    Description = "OAuth相关接口",
                });
                options.DocInclusionPredicate((docName, apiDesc) => apiDesc.GroupName == docName);

                // 添加JWT Bearer认证支持
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
                });

                // options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Snap.Hutao.Server.xml"));
                options.EnableAnnotations();
            });

        // Mvc
        services.AddSingleton(TimeProvider.System);
        services
            .AddControllers(options =>
            {
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                options.Filters.Add(new ServiceFilterAttribute(typeof(ClientVersionTrackFilter)));
            })
            .AddJsonOptions(options =>
            {
                JsonSerializerOptions jsonOptions = options.JsonSerializerOptions;

                jsonOptions.PropertyNamingPolicy = null;
                jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                jsonOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                jsonOptions.PropertyNameCaseInsensitive = true;
                jsonOptions.WriteIndented = false;
                jsonOptions.Converters.Add(new FiniteDoubleJsonConverter());
                jsonOptions.Converters.Add(new FiniteSingleJsonConverter());
            });

        WebApplication app = appBuilder.Build();
        IServiceProvider sp = app.Services;
        _logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
        await MigrateDatabasesAsync(app).ConfigureAwait(false);
        app.UseForwardedHeaders();
        if (!Debugger.IsAttached)
        {
            app.UseSentryTracing();
        }

        // 中间件的顺序敏感
        // https://learn.microsoft.com/zh-cn/aspnet/core/fundamentals/middleware/#middleware-order
        // ExceptionHandler
        app.UseExceptionHandler();

        // HSTS
        // HttpsRedirection
        app.UseHttpsRedirection();

        // Static Files
        app.UseDefaultFiles(new DefaultFilesOptions()
        {
            DefaultFileNames =
            {
                "index.html",
            },
        });
        app.UseStaticFiles();
        app.UseSwagger();

        // app.UseSwaggerUI(options =>
        // {
        //    options.SwaggerEndpoint("/swagger/Public/swagger.json", "PublicApi");
        //    options.SwaggerEndpoint("/swagger/Passport/swagger.json", "账户");
        //    options.SwaggerEndpoint("/swagger/OAuth/swagger.json", "OAuth");
        //    options.SwaggerEndpoint("/swagger/GachaLog/swagger.json", "祈愿记录");
        //    options.SwaggerEndpoint("/swagger/SpiralAbyss/swagger.json", "深渊统计");
        //    options.SwaggerEndpoint("/swagger/RoleCombat/swagger.json", "剧诗统计");
        //    options.SwaggerEndpoint("/swagger/Distribution/swagger.json", "分发管理");
        //    options.SwaggerEndpoint("/swagger/Redeem/swagger.json", "兑换码");
        //    options.SwaggerEndpoint("/swagger/Services/swagger.json", "服务管理");
        // });

        // Routes
        // CORS
        app.UseCors();

        // Authentication
        app.UseAuthentication();

        // Authorization
        app.UseAuthorization();

        // Endpoint
        app.MapControllers();

        app.MapFallbackToFile("admin/{*path:nonfile}", "admin/index.html");

        // Initialize dynamic config from DB
        await sp.GetRequiredService<SystemConfigService>().InitializeAsync();

        IScheduler scheduler = await sp.GetRequiredService<ISchedulerFactory>().GetScheduler(CancellationToken.None).ConfigureAwait(false);
        scheduler.ListenerManager.AddJobListener(sp.GetRequiredService<JobLogListener>(), [GroupMatcher<JobKey>.AnyGroup()]);

        _logger.LogInformation($"Api Version:{applicationVersion}");
        await app.RunAsync();
    }

    /// <summary>
    /// 迁移数据库.
    /// </summary>
    /// <param name="app">app.</param>
    private static async Task MigrateDatabasesAsync(WebApplication app)
    {
        using IServiceScope scope = app.Services.CreateScope();
        AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (appDbContext.Database.IsRelational())
        {
            string[] pending = [.. await appDbContext.Database.GetPendingMigrationsAsync().ConfigureAwait(false)];
            if (pending.Length is not 0)
            {
                appDbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
                await appDbContext.Database.MigrateAsync().ConfigureAwait(false);
                _logger?.LogInformation("Applied {Count} pending migrations: {Migrations}", pending.Length, string.Join(", ", pending));
            }
        }
    }
}
