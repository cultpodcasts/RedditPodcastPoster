using RedditPodcastPoster.Models.Services;

namespace Api.Models;

public class FilmChangeRequest : PublisherFieldsChange
{
    public string? Imdb { get; set; }
    public string? ImdbId { get; set; }
    public int? TmdbId { get; set; }
    public Dictionary<string, ServiceLink>? Services { get; set; }
    public FilmTrailerChange[]? Trailers { get; set; }
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
