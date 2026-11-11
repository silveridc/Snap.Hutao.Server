namespace server.Extension;

internal static class ClientExtension
{
    private const string _snapHutaoClientHeaderPrefix = "Snap Hutao/";

    /// <summary>
    /// Tries to parse the Hutao client version from the controller request.
    /// </summary>
    /// <param name="controller">The current controller.</param>
    /// <param name="version">The parsed client version.</param>
    /// <returns>Whether the version was parsed successfully.</returns>
    public static bool TryGetClientVersion(this ControllerBase controller, [NotNullWhen(true)] out Version? version)
    {
        return controller.HttpContext.TryGetClientVersion(out version);
    }

    /// <summary>
    /// Tries to parse the Hutao client version from the action executing context.
    /// </summary>
    /// <param name="context">The action executing context.</param>
    /// <param name="version">The parsed client version.</param>
    /// <returns>Whether the version was parsed successfully.</returns>
    public static bool TryGetClientVersion(this ActionExecutingContext context, [NotNullWhen(true)] out Version? version)
    {
        return context.HttpContext.TryGetClientVersion(out version);
    }

    /// <summary>
    /// Tries to parse the Hutao client version from the HTTP request User-Agent.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="version">The parsed client version.</param>
    /// <returns>Whether the version was parsed successfully.</returns>
    public static bool TryGetClientVersion(this HttpContext context, [NotNullWhen(true)] out Version? version)
    {
        string? userAgent = context.Request.Headers.UserAgent;

        if (string.IsNullOrEmpty(userAgent) || !userAgent.StartsWith(_snapHutaoClientHeaderPrefix))
        {
            version = default;
            return false;
        }

        return Version.TryParse(userAgent[_snapHutaoClientHeaderPrefix.Length..], out version);
    }
}
