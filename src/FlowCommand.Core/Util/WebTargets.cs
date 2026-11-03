namespace FlowCommand.Core.Util;

/// <summary>
/// URL rules for the website and search actions. Website URLs come only from configuration, never from the AI;
/// the AI supplies nothing but the words to search for, which are percent-encoded into a configured https template.
/// </summary>
public static class WebTargets
{
    public const string DefaultSearchUrl = "https://www.google.com/search?q={query}";
    private const int MaxUrlLength = 2048;

    /// <summary>Accepts absolute http(s) URLs without embedded credentials.</summary>
    public static bool TryNormalizeWebsite(string? url, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;
        if (!string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host)) return false;
        normalized = uri.AbsoluteUri;
        return true;
    }

    /// <summary>Builds a search URL from an https template containing exactly one {query} placeholder.</summary>
    public static bool TryBuildSearchUrl(string? template, string? query, out string url)
    {
        url = "";
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(query)) return false;
        if (!template.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if (template.Split("{query}").Length != 2) return false;
        var encoded = Uri.EscapeDataString(query.Trim());
        return TryNormalizeWebsite(template.Replace("{query}", encoded), out url);
    }
}
