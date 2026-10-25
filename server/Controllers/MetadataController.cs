using server.Models.Metadata;

namespace server.Controllers;

[ApiController]
[Route("[controller]")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class MetadataController(MetadataStoreService metadataStoreService) : ControllerBase
{
    private const string _extension = ".json";

    /// <summary>
    /// 获取元数据文档。
    /// </summary>
    /// <param name="locale">（i18n）语言。</param>
    /// <param name="name">元数据文档名称。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>元数据文档内容。</returns>
    [HttpGet("Genshin/{locale}/{**name}")]
    public async Task<IActionResult> DocumentAsync(string locale, string name, CancellationToken token)
    {
        if (!TryDocumentName(name, out string? documentName))
        {
            return NotFound();
        }

        if (!TryLocale(locale, out string? localeKey))
        {
            return NotFound();
        }

        MetadataDocument? document = await metadataStoreService.FindAsync(localeKey, documentName, token).ConfigureAwait(false);
        if (document is null)
        {
            return NotFound();
        }

        string etag = $"\"{document.Hash}\"";
        if (string.Equals(Request.Headers.IfNoneMatch, etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        return Content(document.Doc, "application/json; charset=utf-8");
    }

    private static bool TryLocale(string locale, [NotNullWhen(true)] out string? key)
    {
        key = null;
        if (locale.Length is < 2 or > 8)
        {
            return false;
        }

        foreach (char character in locale)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z')))
            {
                return false;
            }
        }

        key = locale.ToUpperInvariant();
        return true;
    }

    private static bool TryDocumentName(string name, [NotNullWhen(true)] out string? documentName)
    {
        documentName = null;
        if (!name.EndsWith(_extension, StringComparison.Ordinal))
        {
            return false;
        }

        string candidate = name[..^_extension.Length];
        if (candidate.Length is 0 or > 128 || candidate.StartsWith('/') || candidate.EndsWith('/'))
        {
            return false;
        }

        foreach (char character in candidate)
        {
            bool allowed = character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-' or '.' or '/';
            if (!allowed)
            {
                return false;
            }
        }

        if (candidate.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        documentName = candidate;
        return true;
    }
}
