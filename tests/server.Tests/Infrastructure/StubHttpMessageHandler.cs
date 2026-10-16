using System.Net;
using System.Text;

namespace server.Tests.Infrastructure;

/// <summary>
/// 拦截被测服务对外部网络（enka、Bing、HoYoPlay、reCAPTCHA、GitHub、CDN）的调用，
/// 使测试不依赖外网且结果可复现；未登记的地址返回 502，避免用例意外打到真实站点。
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<Rule> rules = [];

    /// <summary>已收到的请求，用于断言服务确实发起了外部调用。</summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>登记一条匹配规则，后登记的规则优先。</summary>
    public StubHttpMessageHandler Map(Func<HttpRequestMessage, bool> match, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        lock (rules)
        {
            rules.Insert(0, new Rule(match, respond));
        }

        return this;
    }

    /// <summary>
    /// 按 URL 片段登记一条返回固定 JSON 的规则。
    /// </summary>
    /// <param name="bodyFragment">
    /// 额外要求请求体包含该片段，用于让多条同地址规则互不覆盖（如爱发电按订单号回查）。
    /// </param>
    public StubHttpMessageHandler MapJson(string urlFragment, string json, HttpStatusCode status = HttpStatusCode.OK, string? bodyFragment = null)
    {
        return Map(
            request => request.RequestUri?.ToString().Contains(urlFragment, StringComparison.OrdinalIgnoreCase) is true
                && (bodyFragment is null || Contains(request.Content?.ReadAsStringAsync().GetAwaiter().GetResult(), bodyFragment)),
            _ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private static bool Contains(string? text, string fragment)
    {
        return text?.Contains(fragment, StringComparison.Ordinal) is true;
    }

    /// <summary>移除所有规则（用于单个用例内切换外部返回）。</summary>
    public void Reset()
    {
        lock (rules)
        {
            rules.Clear();
        }

        Requests.Clear();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Requests.Add(new RecordedRequest(request.Method.Method, request.RequestUri?.ToString() ?? string.Empty, body));

        Rule[] snapshot;
        lock (rules)
        {
            snapshot = [.. rules];
        }

        foreach (Rule rule in snapshot)
        {
            if (rule.Match(request))
            {
                return rule.Respond(request);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent(
                $"No stub registered for {request.Method} {request.RequestUri}",
                Encoding.UTF8,
                "text/plain"),
        };
    }

    public sealed record RecordedRequest(string Method, string Url, string? Body);

    private sealed record Rule(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond);
}
