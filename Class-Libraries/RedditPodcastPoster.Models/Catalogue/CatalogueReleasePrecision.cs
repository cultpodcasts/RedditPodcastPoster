using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Release precision for non-podcast catalogue playables.
/// Podcast <c>Episode.Release</c> remains a full UTC <see cref="DateTime"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatalogueReleasePrecision
{
    /// <summary>Known year only (typical for some films).</summary>
    Year,

    /// <summary>Calendar date, no time-of-day (TV episodes, news reports, many films).</summary>
    Date
}
