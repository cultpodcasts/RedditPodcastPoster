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
    public bool HasUsableHttpUrl() =>
        Url is not null
        && !string.IsNullOrWhiteSpace(Url.OriginalString)
        && Url.IsAbsoluteUri
        && (Url.Scheme == Uri.UriSchemeHttp || Url.Scheme == Uri.UriSchemeHttps);
}
