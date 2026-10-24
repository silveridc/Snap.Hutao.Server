using System.Text.RegularExpressions;
using server.Models.Admin;
using server.Models.Entity;
using server.Services.Admin;

namespace server.Controllers.Filter;

// Transient：记录后台维护者的写操作（POST）到审计日志
public sealed partial class AuditLogActionFilter(IServiceScopeFactory scopeFactory, ILogger<AuditLogActionFilter> logger, TimeProvider timeProvider) : IAsyncActionFilter
{
    private const int _maxDetailLength = 2000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ActionExecutedContext executed = await next().ConfigureAwait(false);

        // 只记录真正执行了的写操作（POST），跳过被权限过滤器短路的请求
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method) || executed.Canceled)
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
                Detail = Redact(SerializeArgs(context.ActionArguments)),
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
            string json = JsonSerializer.Serialize(args);
            return json.Length > _maxDetailLength ? json[.._maxDetailLength] : json;
        }
        catch
        {
            return null;
        }
    }

    private static string? Redact(string? detail)
    {
        return string.IsNullOrEmpty(detail) ? detail : PasswordRegex().Replace(detail, "\"$1\":\"***\"");
    }

    [GeneratedRegex("\"(\\w*[Pp]assword\\w*)\"\\s*:\\s*\"[^\"]*\"")]
    private static partial Regex PasswordRegex();
}
