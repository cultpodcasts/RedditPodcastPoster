using System.Diagnostics.CodeAnalysis;

namespace Api.Models;

public class SubmitUrlRequest
{
    public required Uri Url { get; set; }

    public Guid? PodcastId { get; set; }

    public string? PodcastName { get; set; }

    /// <summary>
    /// Isolated <c>required Uri</c> only means the JSON property was present.
    /// Submit needs a non-blank absolute http(s) URL.
    /// </summary>
    public bool HasUsableHttpUrl() => IsUsableHttpUrl(Url);

    public static bool TryParseUsableHttpUrl(string? value, [NotNullWhen(true)] out Uri url)
    {
        url = null!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.RelativeOrAbsolute, out var parsed))
        {
            return false;
        }

        if (!IsUsableHttpUrl(parsed))
        {
            return false;
        }

        url = parsed;
        return true;
    }

    public static bool IsUsableHttpUrl(Uri? url) =>
        url is not null
        && !string.IsNullOrWhiteSpace(url.OriginalString)
        && url.IsAbsoluteUri
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps);
}
