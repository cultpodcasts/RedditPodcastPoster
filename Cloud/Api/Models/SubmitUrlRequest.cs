using System.Diagnostics.CodeAnalysis;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;

namespace Api.Models;

public class SubmitUrlRequest
{
    public required Uri Url { get; set; }

    public Guid? PodcastId { get; set; }

    public string? PodcastName { get; set; }

    /// <summary><c>NewsOrganisation</c> or <c>TvShow</c>, case-insensitive (website hub Submit URL).</summary>
    public string? ParentKind { get; set; }

    public Guid? ParentId { get; set; }

    public string? ParentName { get; set; }

    /// <summary>
    /// Worker-injected streaming metadata from StreamMeta KV after prepare/extract
    /// (<c>submitUsesPrefetchedMetaWhenCached</c>). Azure trusts the authenticated Worker
    /// caller — do not forward SPA-supplied meta on public submit bodies.
    /// </summary>
    public NonPodcastServiceItemMetaData? PrefetchedMeta { get; set; }

    /// <summary>
    /// Isolated <c>required Uri</c> only means the JSON property was present.
    /// Submit needs a non-blank absolute http(s) URL.
    /// </summary>
    public bool HasUsableHttpUrl() => IsUsableHttpUrl(Url);

    /// <summary>True when any parent-target field was sent.</summary>
    public bool HasParentTarget() =>
        !string.IsNullOrWhiteSpace(ParentKind) || ParentId != null || !string.IsNullOrWhiteSpace(ParentName);

    /// <summary>
    /// The explicit news organisation / TV show target, or null when none was sent. <paramref name="error"/> is set
    /// (and the result null) for an unknown kind, a missing / empty id, or parent and podcast fields together.
    /// </summary>
    public CatalogueParentTarget? GetParentTarget(out string? error)
    {
        error = null;
        if (!HasParentTarget())
        {
            return null;
        }

        if (PodcastId != null || !string.IsNullOrWhiteSpace(PodcastName))
        {
            error = "Send either parentKind/parentId/parentName or podcastId/podcastName, not both.";
            return null;
        }

        var kindText = ParentKind?.Trim();
        if (string.IsNullOrEmpty(kindText) || kindText.Any(char.IsDigit) ||
            !Enum.TryParse<CatalogueParentKind>(kindText, ignoreCase: true, out var kind) ||
            !Enum.IsDefined(kind))
        {
            error = "parentKind must be 'NewsOrganisation' or 'TvShow'.";
            return null;
        }

        if (ParentId is not { } id || id == Guid.Empty)
        {
            error = "parentId is required with parentKind.";
            return null;
        }

        return new CatalogueParentTarget(kind, id, string.IsNullOrWhiteSpace(ParentName) ? null : ParentName.Trim());
    }

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
