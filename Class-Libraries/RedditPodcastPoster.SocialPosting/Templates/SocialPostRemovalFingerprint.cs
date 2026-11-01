using System.Globalization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlShortening.Extensions;

namespace RedditPodcastPoster.SocialPosting.Templates;

/// <summary>
/// How the tweet and Bluesky removers recognise the post built for an item. It is built from the same pieces as
/// the post templates (<see cref="SocialPostKindTemplate"/>), so the two can't drift.
/// <list type="bullet">
/// <item>Podcast episode: the post contains the podcast name, the <c>d MMM yyyy</c> release and the
/// <c>[h:mm:ss]</c> length (unchanged).</item>
/// <item>TV episode / news report: the post links the item's site page (matched on its short id, so a renamed
/// parent still matches), or contains the parent name and the template's <see cref="SocialPostKindTemplate.ReleaseLine"/>
/// (year-only release, no zero length).</item>
/// <item>Film: the post links the film's site page. The title can't be matched, because the post re-cases,
/// hashtag-enriches and may truncate it, and a film has no parent line.</item>
/// </list>
/// Twitter returns links as t.co in the text, so pass the tweet's expanded URLs as <c>linkedUrls</c>.
/// Shared with the curator post-status retraction (#1053), which should build its target from this.
/// </summary>
public sealed class SocialPostRemovalFingerprint
{
    private readonly Regex? _siteLink;
    private readonly IReadOnlyList<IReadOnlyList<string>> _textFragmentSets;

    private SocialPostRemovalFingerprint(Guid id, IReadOnlyList<IReadOnlyList<string>> textFragmentSets,
        string? siteLinkShortId)
    {
        Id = id;
        _textFragmentSets = textFragmentSets;
        TextFragments = textFragmentSets.Count > 0 ? textFragmentSets[0] : [];
        SiteLinkShortId = siteLinkShortId;
        if (siteLinkShortId != null)
        {
            // Short ids are URL-safe base64, so the id must not run on into more base64 characters.
            _siteLink = new Regex("/" + Regex.Escape(siteLinkShortId) + "(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant);
        }
    }

    /// <summary>Item id, for logs.</summary>
    public Guid Id { get; }

    /// <summary>
    /// Text a post must contain all of. Empty for a film. A podcast-episode post may instead match the same
    /// fragments with the release date in the invariant culture (see <see cref="For"/>).
    /// </summary>
    public IReadOnlyList<string> TextFragments { get; }

    /// <summary>Short id at the end of the catalogue site link; null for a podcast episode.</summary>
    public string? SiteLinkShortId { get; }

    /// <param name="item">The item the post was made for.</param>
    /// <param name="postedParentName">
    /// The parent name as the poster wrote it (the tweet and Bluesky builders write
    /// <c>ITextSanitiser.SanitisePodcastName</c>). Null or blank uses <c>item.Podcast.Name</c>.
    /// </param>
    public static SocialPostRemovalFingerprint For(PodcastEpisode item, string? postedParentName = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        var episode = item.Episode;
        var parentName = string.IsNullOrWhiteSpace(postedParentName) ? item.Podcast.Name : postedParentName;
        if (!item.IsCataloguePlayable)
        {
            var length = episode.Length.ToString(SocialPostKindTemplate.LengthFormat, CultureInfo.InvariantCulture);
            // The podcast release line is written in the host's culture (SocialPostKindTemplate.ReleaseLine), so a
            // post made under one culture is still found when removed under another (e.g. "Sept" vs "Sep").
            var release = episode.ReleaseUtc.ToString(SocialPostKindTemplate.ReleaseFormat);
            var invariantRelease =
                episode.ReleaseUtc.ToString(SocialPostKindTemplate.ReleaseFormat, CultureInfo.InvariantCulture);
            IReadOnlyList<IReadOnlyList<string>> sets = release == invariantRelease
                ? [[parentName, length, release]]
                : [[parentName, length, release], [parentName, length, invariantRelease]];
            return new SocialPostRemovalFingerprint(episode.Id, sets, null);
        }

        IReadOnlyList<IReadOnlyList<string>> fragments = SocialPostKindTemplate.HasParentLine(item)
            ? [[parentName, SocialPostKindTemplate.ReleaseLine(item)]]
            : [];
        return new SocialPostRemovalFingerprint(episode.Id, fragments,
            CatalogueShortId.Encode(episode.Id, item.ContentKind));
    }

    /// <summary>True when the post with <paramref name="text"/> (and <paramref name="linkedUrls"/>) is this item's.</summary>
    public bool Matches(string? text, IEnumerable<string?>? linkedUrls = null)
    {
        if (_siteLink != null &&
            ((text != null && _siteLink.IsMatch(text)) ||
             (linkedUrls?.Any(url => url != null && _siteLink.IsMatch(url)) ?? false)))
        {
            return true;
        }

        return !string.IsNullOrEmpty(text) &&
               _textFragmentSets.Any(set => set.Count > 0 &&
                                            set.All(fragment => text.Contains(fragment, StringComparison.Ordinal)));
    }
}
