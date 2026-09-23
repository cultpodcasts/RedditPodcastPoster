namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// How a <see cref="CatalogueRelease"/> is stored in JSON (the wire value encodes precision).
/// </summary>
public enum CatalogueReleasePrecision
{
    /// <summary>JSON number, e.g. <c>2020</c>.</summary>
    Year,

    /// <summary>JSON string calendar date, e.g. <c>"2020-06-15"</c>.</summary>
    Date,

    /// <summary>JSON string ISO-8601 UTC with Z, e.g. <c>"2020-06-15T12:34:56Z"</c>.</summary>
    DateTimeUtc
}
