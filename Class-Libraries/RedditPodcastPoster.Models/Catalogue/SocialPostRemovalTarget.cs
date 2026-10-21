using System.Globalization;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>One piece of text a post must contain to be the post for an item.</summary>
public sealed record SocialPostTextFragment(string Value, bool IgnoreCase = false)
{
    public bool IsIn(string text) =>
        text.Contains(Value, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

/// <summary>
/// What the tweet and Bluesky removers need to find a post: the item id (for logs), the text fragments the post
/// was built with, and the Bluesky AT URI when one was stored.
/// The fragments follow the per-kind post templates (<c>SocialPostKindTemplate</c> in #1051):
/// <list type="bullet">
/// <item>Podcast episode: podcast name, <c>d MMM yyyy</c> release, <c>[h:mm:ss]</c> length (unchanged).</item>
/// <item>TV episode / news report: show or organisation name line; release as the year for a year-only release,
/// otherwise <c>d MMM yyyy</c>; length only when above zero.</item>
/// <item>Film: no parent line; release and length as above, plus the opening characters of the title
/// (case-insensitive, because the post title is re-cased and may be truncated).</item>
/// </list>
/// </summary>
public sealed record SocialPostRemovalTarget(
    Guid Id,
    IReadOnlyList<SocialPostTextFragment> TextFragments,
    string? BlueskyPost)
{
    public const string ReleaseFormat = "d MMM yyyy";
    public const string LengthFormat = @"\[h\:mm\:ss\]";

    /// <summary>The post templates never truncate a title below this many characters.</summary>
    public const int FilmTitlePrefixLength = 10;

    /// <summary>True when <paramref name="text"/> carries every fragment.</summary>
    public bool Matches(string? text) =>
        !string.IsNullOrEmpty(text) && TextFragments.Count > 0 && TextFragments.All(f => f.IsIn(text));

    public static SocialPostRemovalTarget From(PodcastEpisode podcastEpisode) =>
        new(podcastEpisode.Episode.Id,
        [
            new SocialPostTextFragment(podcastEpisode.Podcast.Name),
            new SocialPostTextFragment(podcastEpisode.Episode.Length.ToString(LengthFormat, CultureInfo.InvariantCulture)),
            new SocialPostTextFragment(podcastEpisode.Episode.ReleaseUtc.ToString(ReleaseFormat))
        ],
            podcastEpisode.Episode.BlueskyPost);

    /// <summary>TV episode or news report: <paramref name="parentName"/> is the show or organisation line.</summary>
    public static SocialPostRemovalTarget ForCatalogueChild(
        Guid id,
        string parentName,
        CatalogueRelease? release,
        DateTime releaseSort,
        TimeSpan length,
        string? blueskyPost)
    {
        List<SocialPostTextFragment> fragments = [new(parentName)];
        fragments.AddRange(ReleaseAndLength(release, releaseSort, length));
        return new SocialPostRemovalTarget(id, fragments, blueskyPost);
    }

    /// <summary>Film: no parent line, so the title prefix stands in for it.</summary>
    public static SocialPostRemovalTarget ForFilm(
        Guid id,
        string name,
        CatalogueRelease? release,
        DateTime releaseSort,
        TimeSpan length,
        string? blueskyPost)
    {
        var title = name.Replace("#", string.Empty).Replace("@", string.Empty).Trim();
        List<SocialPostTextFragment> fragments =
            [new("\"" + title[..Math.Min(title.Length, FilmTitlePrefixLength)], IgnoreCase: true)];
        fragments.AddRange(ReleaseAndLength(release, releaseSort, length));
        return new SocialPostRemovalTarget(id, fragments, blueskyPost);
    }

    private static IEnumerable<SocialPostTextFragment> ReleaseAndLength(
        CatalogueRelease? release, DateTime releaseSort, TimeSpan length)
    {
        yield return new SocialPostTextFragment(release?.Precision == CatalogueReleasePrecision.Year
            ? release.Year.ToString(CultureInfo.InvariantCulture)
            : releaseSort.ToString(ReleaseFormat, CultureInfo.InvariantCulture));
        if (length > TimeSpan.Zero)
        {
            yield return new SocialPostTextFragment(length.ToString(LengthFormat, CultureInfo.InvariantCulture));
        }
    }
}
