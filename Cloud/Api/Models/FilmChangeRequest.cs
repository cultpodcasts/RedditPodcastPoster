using RedditPodcastPoster.Models.Services;

namespace Api.Models;

public class FilmChangeRequest : PublisherFieldsChange
{
    public string? Imdb { get; set; }
    public string? ImdbId { get; set; }
    public int? TmdbId { get; set; }
    public Dictionary<string, ServiceLink>? Services { get; set; }
    public FilmTrailerChange[]? Trailers { get; set; }

    /// <summary>
    /// Trailer ids the client last saw. Required when <see cref="Trailers"/> is sent: a stored trailer whose id is
    /// not here is a 409, so a stale keep-list cannot delete a trailer this client never listed. Null and empty
    /// both mean the client listed none.
    /// </summary>
    public Guid[]? ExpectedTrailerIds { get; set; }
    public string? Release { get; set; }
    public string? Duration { get; set; }
    public bool? Explicit { get; set; }
    public bool? Ignored { get; set; }
    public bool? Posted { get; set; }
    public bool? Tweeted { get; set; }
    public bool? UnBluesky { get; set; }
    public string[]? Subjects { get; set; }
    public string[]? Guests { get; set; }
}
