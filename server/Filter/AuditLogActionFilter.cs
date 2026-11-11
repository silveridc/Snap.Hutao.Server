using server.Models.Admin;
using server.Models.Entity;
using server.Services.Admin;

namespace server.Controllers.Filter;

// Transient
public sealed class AuditLogActionFilter(IServiceScopeFactory scopeFactory, ILogger<AuditLogActionFilter> logger, TimeProvider timeProvider) : IAsyncActionFilter
{
    private const int _maxDetailLength = 2000;

    /// <summary>
    /// Records executed write operations (POST/PUT/PATCH/DELETE) into the admin audit log. Requests short-circuited by
    /// permission filters are skipped, and audit failures never affect the business request.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="next">The next action delegate.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ActionExecutedContext executed = await next().ConfigureAwait(false);

        // 只记录真正执行了的写操作（POST/PUT/PATCH/DELETE），跳过被权限过滤器短路的请求
        string method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || executed.Canceled)
        {
            return;
        }

        try
        {
            context.HttpContext.TryGetUserId(out int userId);
            context.HttpContext.TryGetAdminAccountId(out int accountId);

            using IServiceScope scope = scopeFactory.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            AdminAuditLog log = new()
            {
                AdminUserId = userId,
                AdminAccountId = accountId > 0 ? accountId : null,
                AdminAccountName = await GetAdminAccountNameAsync(scope.ServiceProvider, accountId).ConfigureAwait(false),
                Action = context.HttpContext.Request.Path.Value ?? string.Empty,
                Detail = SerializeArgs(context.ActionArguments),
                Ip = context.HttpContext.GetClientIp(),
                Success = executed.Exception is null,
                Timestamp = timeProvider.GetUnixTimeSeconds(),
            };

            db.AdminAuditLogs.Add(log);
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 审计失败绝不能影响业务请求
            logger.LogWarning(ex, "Failed to write admin audit log");
        }
    }

    private static async Task<string?> GetAdminAccountNameAsync(IServiceProvider serviceProvider, int accountId)
    {
        if (accountId is 0)
        {
            return null;
        }

        // 走 15 分钟缓存：账号名可能已在后台改过，快照写进日志才认得出是谁
        AdminAccountState? state = await serviceProvider.GetRequiredService<AdminSsoService>().GetAccountStateAsync(accountId).ConfigureAwait(false);
        return state?.UserName;
    }

    private static string? SerializeArgs(IDictionary<string, object?> args)
    {
        if (args.Count == 0)
        {
            return null;
        }

        try
        {
            JsonElement data = JsonSerializer.SerializeToElement(args);
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                WriteRedacted(writer, data);
            }

            // 先脱敏再截断，避免秘密值被截在一半后绕过清洗
            string json = Encoding.UTF8.GetString(buffer.ToArray());
            return json.Length > _maxDetailLength ? json[.._maxDetailLength] : json;
        }
        catch
        {
            return null;
        }
    }

    private static void WriteRedacted(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (SystemSettingSecurity.IsSensitiveKey(property.Name))
                    {
                        writer.WriteStringValue("***");
                    }
                    else
                    {
                        WriteRedacted(writer, property.Value);
                    }
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                {
                    WriteRedacted(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
