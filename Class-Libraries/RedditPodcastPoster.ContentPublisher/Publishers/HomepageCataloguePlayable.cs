using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

/// <summary>
/// One recent news report, TV show episode or film for the homepage, in the same shape the podcast
/// episodes use. <see cref="ParentName"/> is the news organisation or TV show name; a film has no parent,
/// so it is the film's own name.
/// </summary>
public sealed record HomepageCataloguePlayable
{
    /// <summary><c>NewsReport</c>, <c>TvShowEpisode</c> or <c>Film</c> (<see cref="Models.ContentKinds.ContentKind"/>).</summary>
    public required string ContentKind { get; init; }
    public required Guid Id { get; init; }
    public required string ParentName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime Release { get; init; }
    public TimeSpan Length { get; init; }
    public Dictionary<string, ServiceLink>? Services { get; init; }
    public EpisodeIds? Ids { get; init; }
    public EpisodeImages? Images { get; init; }
    public List<string> Subjects { get; init; } = [];
    public string? Language { get; init; }
    public string TitleRegex { get; init; } = string.Empty;
    public string DescriptionRegex { get; init; } = string.Empty;
    public string[]? KnownTerms { get; init; }
}
