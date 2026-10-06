namespace server.Controllers
{
    internal static class ClientExtension
    {
        private const string SnapHutaoClientHeaderPrefix = "Snap Hutao/";

        /// <summary>
        /// 尝试从控制器的请求中解析胡桃客户端版本。
        /// </summary>
        /// <param name="controller">当前控制器。</param>
        /// <param name="version">解析出的客户端版本。</param>
        /// <returns>是否解析成功。</returns>
        public static bool TryGetClientVersion(this ControllerBase controller, [NotNullWhen(true)] out Version? version)
        {
            return controller.HttpContext.TryGetClientVersion(out version);
        }

        /// <summary>
        /// 尝试从操作执行上下文中解析胡桃客户端版本。
        /// </summary>
        /// <param name="context">操作执行上下文。</param>
        /// <param name="version">解析出的客户端版本。</param>
        /// <returns>是否解析成功。</returns>
        public static bool TryGetClientVersion(this ActionExecutingContext context, [NotNullWhen(true)] out Version? version)
        {
            return context.HttpContext.TryGetClientVersion(out version);
        }

        /// <summary>
        /// 尝试从 HTTP 请求的 UserAgent 中解析胡桃客户端版本。
        /// </summary>
        /// <param name="context">HTTP 上下文。</param>
        /// <param name="version">解析出的客户端版本。</param>
        /// <returns>是否解析成功。</returns>
        public static bool TryGetClientVersion(this HttpContext context, [NotNullWhen(true)] out Version? version)
        {
            string? userAgent = context.Request.Headers.UserAgent;

            if (string.IsNullOrEmpty(userAgent) || !userAgent.StartsWith(SnapHutaoClientHeaderPrefix))
            {
                version = default;
                return false;
            }

            return Version.TryParse(userAgent[SnapHutaoClientHeaderPrefix.Length..], out version);
        }
    }
}