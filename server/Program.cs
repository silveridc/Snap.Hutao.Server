using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using Sentry;
using Sentry.Protocol;
using server.Extension;
using server.Services.Admin;
using server.Services.Cache;
using server.Services.WallPaper;
using StackExchange.Redis;


namespace server
{
    public static class Program
    {
        private static ILogger? logger;
        public const string applicationVersion = "server@v1.0.4";
        private const string sentryProjectId = "7";
        private const string sentryDsn = "https://26b2259ad03e4fd26fedf7b08226eaa2@sentry.silveridc.cn/7";
        private const string otlpToken = "sentry sentry_key=26b2259ad03e4fd26fedf7b08226eaa2";
        public static async Task Main(string[] args)
        {
            WebApplicationBuilder appBuilder = WebApplication.CreateBuilder(args);
            AppOptions appOptions = appBuilder.Configuration.GetSection("App").Get<AppOptions>()!;
            IServiceCollection services = appBuilder.Services;
            IConnectionMultiplexer redisMultiplexer = ConnectionMultiplexer.Connect(appOptions.RedisAddress);
            services.AddSingleton(redisMultiplexer);

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
                options.Dsn = sentryDsn;
                options.TracesSampleRate = 1D;
                options.SendDefaultPii = true;
                options.Debug = true;
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
                        otlp.Endpoint = new Uri($"https://sentry.silveridc.cn/api/{sentryProjectId}/integration/otlp/v1/traces");
                        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                        otlp.HttpClientFactory = () =>
                        {
                            HttpClient client = new();
                            client.DefaultRequestHeaders.Add("x-sentry-auth", otlpToken);
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
                    otlp.Endpoint = new Uri($"https://sentry.silveridc.cn/api/{sentryProjectId}/integration/otlp/v1/logs");
                    otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
                    otlp.HttpClientFactory = () =>
                    {
                        HttpClient client = new();
                        client.DefaultRequestHeaders.Add("x-sentry-auth", otlpToken);
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

            // Services
            services
                .AddAuthorization()
                .AddCors(options => options.AddDefaultPolicy(builder => builder.AllowAnyMethod().AllowAnyHeader().AllowAnyOrigin()))
                .AddDbContextPool<AppDbContext>((serviceProvider, options) =>
                {
                    string connectionString = appBuilder.Configuration.GetConnectionString("PrimaryMysql8")!;

                    ILogger<AppDbContext> logger = serviceProvider.GetRequiredService<ILogger<AppDbContext>>();
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("AppDbContext Using connection string: [{Constr}]", connectionString);
                    }

                    options
                        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                        .ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)))
                        .EnableSensitiveDataLogging()
                        .EnableDetailedErrors();
                })
                .AddDbContextPool<MetadataDbContext>((serviceProvider, options) =>
                {
                    string connectionString = appBuilder.Configuration.GetConnectionString("MetadataMysql8")!;

                    ILogger<MetadataDbContext> logger = serviceProvider.GetRequiredService<ILogger<MetadataDbContext>>();
                    if (logger.IsEnabled(LogLevel.Information))
                    {
                        logger.LogInformation("MetadataDbContext Using connection string: [{Constr}]", connectionString);
                    }

                    options
                        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                        .ConfigureWarnings(c => c.Log((RelationalEventId.CommandExecuted, LogLevel.Debug)))
                        .EnableSensitiveDataLogging()
                        .EnableDetailedErrors();
                })
                .AddEndpointsApiExplorer()
                .AddHttpClient()
                .AddStackExchangeRedisCache(options =>
                {
                    options.ConnectionMultiplexerFactory = () => Task.FromResult(redisMultiplexer);
                    options.InstanceName = DistributedCacheExtension.InstanceName;
                })
                .AddProblemDetails(options =>
                {
                    options.CustomizeProblemDetails = context =>
                    {
                        context.ProblemDetails.Extensions["retcode"] = context.ProblemDetails.Status ?? (int)ReturnCode.InternalStateException;
                        context.ProblemDetails.Extensions["message"] = context.Exception?.Message ?? string.Empty;
                    };
                })
                .AddQuartz(config =>
                {
                    config.ScheduleJob<GachaLogStatisticsRefreshJob>(t => t.StartNow().WithCronSchedule("0 30 */1 * * ?"));
                    config.ScheduleJob<LegacyStatisticsRefreshJob>(t => t.StartNow().WithCronSchedule("0 5 */1 * * ?"));
                    config.ScheduleJob<SpiralAbyssRecordCleanJob>(t => t.StartNow().WithCronSchedule("0 0 4 16 * ?"));
                    config.ScheduleJob<RoleCombatStatisticsRefreshJob>(t => t.StartNow().WithCronSchedule("0 10 */1 * * ?"));
                    config.ScheduleJob<RoleCombatRecordCleanJob>(t => t.StartNow().WithCronSchedule("0 0 4 1 * ?"));
               })
                .AddQuartzHostedService(options => options.WaitForJobsToComplete = true)
                .AddResponseCompression()

                .AddScoped<AccessionService>()
                .AddScoped<AnnouncementService>()
                .AddScoped<DistributionService>()
                .AddScoped<GachaLogService>()
                .AddScoped<GithubService>()
                .AddKeyedScoped<IOAuthProvider, GithubService>(OAuthProviderKind.Github)
                .AddScoped<PassportService>()
                .AddScoped<PassportVerificationService>()
                .AddScoped<PizzaHelperRecordService>()
                .AddScoped<RecordService>()
                .AddScoped<SpiralAbyssStatisticsService>()
                .AddScoped<TelemetryService>()
                .AddScoped<RedeemService>()
                .AddScoped<UserAdminService>()
                .AddScoped<BanService>()
                .AddScoped<AdminQueryService>()
                .AddScoped<ReleaseService>()
                .AddScoped<BingWallpaper>()
                .AddScoped<HoyoPlayWallpaper>()
                .AddScoped<GithubApiService>()
                .AddScoped<StatisticsService>()
                .AddScoped<ReCaptchaService>()
                .AddScoped<RoleCombatService>()
                .AddScoped<AuditLogActionFilter>()
                .AddScoped<ValidateCdnPermission>()
                .AddScoped<ValidateGachaLogPermission>()
                .AddScoped<ValidateMaintainPermission>()
                .AddScoped<GachaLogStatisticsService>()

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
                .AddTransient<GachaLogStatisticsRefreshJob>()
                .AddTransient<LegacyStatisticsRefreshJob>()
                .AddTransient<RoleCombatRecordCleanJob>()
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
                        ValidIssuer = appOptions.JWTIssuer,
                        ValidateAudience = false,
                        RequireExpirationTime = true,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(10),
                    };
                });

            // Identity
            services
                .AddIdentityCore<HutaoUser>(options =>
                {
                    PasswordOptions passwordOptions = options.Password;

                    passwordOptions.RequiredLength = 8;
                    passwordOptions.RequiredUniqueChars = 0;
                    passwordOptions.RequireLowercase = false;
                    passwordOptions.RequireUppercase = false;
                    passwordOptions.RequireNonAlphanumeric = false;
                    passwordOptions.RequireDigit = false;
                })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();
            // Swagger
            services
                .AddEndpointsApiExplorer()
                .AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new() { Version = "v1", Title = "Api v1", Description = "公共api" });
                    options.SwaggerDoc("SpiralAbyss", new() { Version = "1.0", Title = "深渊统计", Description = "深渊统计数据" });
                    options.SwaggerDoc("RoleCombat", new() { Version = "1.0", Title = "剧诗统计", Description = "幻想真境剧诗" });
                    options.SwaggerDoc("Passport", new() { Version = "1.0", Title = "账户", Description = "账户" });
                    options.SwaggerDoc("GachaLog", new() { Version = "1.0", Title = "祈愿记录", Description = "账户祈愿记录管理" });
                    options.SwaggerDoc("Distribution", new() { Version = "1.0", Title = "分发管理", Description = "分发管理" });
                    options.SwaggerDoc("Services", new() { Version = "1.0", Title = "服务管理", Description = "维护专用管理接口，调用需要维护权限" });
                    options.SwaggerDoc("Redeem", new() { Version = "1.0", Title = "兑换码", Description = "兑换码相关接口" });
                    options.SwaggerDoc("OAuth", new() { Version = "1.0", Title = "OAuth", Description = "OAuth相关接口" });
                    options.DocInclusionPredicate((docName, apiDesc) => docName == "v1" ? apiDesc.GroupName is null : apiDesc.GroupName == docName);

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

                    //options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Snap.Hutao.Server.xml"));
                    options.EnableAnnotations();
                });
            // Mvc
            services
                .AddControllers(options =>
                {
                    // Disable non-nullable as [Required]
                    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                })
                .AddJsonOptions(options =>
                {
                    JsonSerializerOptions jsonOptions = options.JsonSerializerOptions;

                    jsonOptions.PropertyNamingPolicy = null;
                    jsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    jsonOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                    jsonOptions.PropertyNameCaseInsensitive = true;
                    jsonOptions.WriteIndented = false;
                });

            WebApplication app = appBuilder.Build();
            logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
            MigrateDatabase(app);
            app.UseForwardedHeaders();
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
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Api v1");
                options.SwaggerEndpoint("/swagger/Passport/swagger.json", "账户");
                options.SwaggerEndpoint("/swagger/OAuth/swagger.json", "OAuth");
                options.SwaggerEndpoint("/swagger/GachaLog/swagger.json", "祈愿记录");
                options.SwaggerEndpoint("/swagger/SpiralAbyss/swagger.json", "深渊统计");
                options.SwaggerEndpoint("/swagger/RoleCombat/swagger.json", "剧诗统计");
                options.SwaggerEndpoint("/swagger/Distribution/swagger.json", "分发管理");
                options.SwaggerEndpoint("/swagger/Redeem/swagger.json", "兑换码");
                options.SwaggerEndpoint("/swagger/Services/swagger.json", "服务管理");
            });

            // Routes
            // CORS
            app.UseCors();

            // Authentication
            app.UseAuthentication();

            // Authorization
            app.UseAuthorization();

            // Endpoint
            app.MapControllers();

            // Initialize dynamic config from DB
            await app.Services.GetRequiredService<SystemConfigService>().InitializeAsync();
            logger.LogInformation($"Api Version:{applicationVersion}");
            await app.RunAsync();
        }

        /// <summary>
        /// 迁移数据库
        /// </summary>
        /// <param name="app">app</param>
        [Conditional("RELEASE")]
        private static void MigrateDatabase(WebApplication app)
        {
            using IServiceScope scope = app.Services.CreateScope();
            AppDbContext context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (context.Database.IsRelational() && context.Database.GetPendingMigrations().Any())
            {
                context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
                context.Database.Migrate();
            }
        }
    }
}