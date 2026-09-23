using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// Consumable catalogue playable (Episode, TvShowEpisode, Film, NewsReport):
/// runtime metadata, subjects, guests, and platform service links.
/// Blurb/language/search terms are <see cref="ICatalogueCopy"/> (on Publisher and Playable).
/// </summary>
public interface IPlayable
{
    TimeSpan Length { get; set; }

    bool Explicit { get; set; }

    List<string> Subjects { get; set; }

    List<string> RemovedSubjects { get; set; }

    string[]? Guests { get; set; }

    Dictionary<string, ServiceLink>? Services { get; set; }
}
