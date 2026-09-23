using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Consumable catalogue playable (Episode, TvShowEpisode, Film, NewsReport).
/// Film satisfies Description / Language / SearchTerms via <see cref="Publisher"/>.
/// </summary>
public interface IPlayable
{
    string Description { get; set; }

    TimeSpan Length { get; set; }

    bool Explicit { get; set; }

    string? Language { get; set; }

    string? SearchTerms { get; set; }

    List<string> Subjects { get; set; }

    List<string> RemovedSubjects { get; set; }

    string[]? Guests { get; set; }

    Dictionary<string, ServiceLink>? Services { get; set; }
}
