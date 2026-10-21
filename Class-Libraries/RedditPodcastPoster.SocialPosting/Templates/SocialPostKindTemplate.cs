using System.Globalization;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.SocialPosting.Templates;

/// <summary>
/// Per-kind pieces shared by the Twitter and Bluesky post templates.
/// Podcast episode: podcast name line, "d MMM yyyy [h:mm:ss]", site link only when there is more than one service or a subject.
/// TV episode / news report: show or organisation name line, same date line, always the /tv or /news site link.
/// Film: no parent line (a film has no parent, and its name is already the title), release at its own precision,
/// always the /film site link.
/// </summary>
public static class SocialPostKindTemplate
{
    public const string ReleaseFormat = "d MMM yyyy";
    public const string LengthFormat = @"\[h\:mm\:ss\]";

    /// <summary>False for a film, whose name is already the quoted title.</summary>
    public static bool HasParentLine(PodcastEpisode item) => !item.IsFilm;

    /// <summary>
    /// TV episodes, news reports and films always carry their site link: the kind page is their home
    /// and the podcast "multiple services or subjects" rule does not apply.
    /// </summary>
    public static bool AlwaysLinksSite(PodcastEpisode item) => item.IsCataloguePlayable;

    /// <summary>
    /// Release and length line. A year-only or date-only catalogue release is never shown as a made-up
    /// "1 Jan" day, and an unknown (zero) catalogue length is left out.
    /// </summary>
    public static string ReleaseLine(PodcastEpisode item)
    {
        var episode = item.Episode;
        if (!item.IsCataloguePlayable)
        {
            // Podcast line unchanged.
            return $"{episode.ReleaseUtc.ToString(ReleaseFormat)} {episode.Length.ToString(LengthFormat, CultureInfo.InvariantCulture)}";
        }

        var release = episode.Release?.Precision == CatalogueReleasePrecision.Year
            ? episode.Release.Year.ToString(CultureInfo.InvariantCulture)
            : episode.ReleaseUtc.ToString(ReleaseFormat, CultureInfo.InvariantCulture);
        if (episode.Length <= TimeSpan.Zero)
        {
            return release;
        }

        return $"{release} {episode.Length.ToString(LengthFormat, CultureInfo.InvariantCulture)}";
    }
}
