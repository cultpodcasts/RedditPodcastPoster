using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Apple.Models;

public class ResolvedAppleItem
{
    public ResolvedAppleItem(long? showId,
        long? episodeId,
        string showName,
        string showDescription,
        string publisher,
        string episodeTitle,
        string episodeDescription,
        DateTime release,
        TimeSpan duration,
        Uri url,
        bool @explicit,
        Uri? image)
    {
        ShowId = showId;
        EpisodeId = episodeId;
        ShowName = showName;
        ShowDescription = showDescription;
        Publisher = publisher;
        EpisodeTitle = episodeTitle;
        EpisodeDescription = episodeDescription;
        Release = release;
        Duration = duration;
        Url = url;
        Explicit = @explicit;
        Image = image;
    }

    public ResolvedAppleItem(RedditPodcastPoster.Models.Episodes.PodcastEpisode podcastEpisode)
    {
        ShowId = podcastEpisode.Podcast.AppleId;
        EpisodeId = EpisodeServicePresence.AppleEpisodeId(podcastEpisode.Episode);
        ShowName = podcastEpisode.Podcast.Name;
        Publisher = podcastEpisode.Podcast.Publisher;
        EpisodeTitle = podcastEpisode.Episode.Title;
        EpisodeDescription = podcastEpisode.Episode.Description;
        Release = podcastEpisode.Episode.Release;
        Duration = podcastEpisode.Episode.Length;
        Explicit = podcastEpisode.Episode.Explicit;
        Url = EpisodeServicePresence.TryGetUrl(podcastEpisode.Episode, ServiceKeys.Apple);
        Image = EpisodeServicePresence.TryGetImage(podcastEpisode.Episode, ServiceKeys.Apple);
    }

    public long? ShowId { get; init; }
    public long? EpisodeId { get; init; }
    public string ShowName { get; init; }
    public string ShowDescription { get; init; } = string.Empty;
    public string Publisher { get; init; }
    public string EpisodeTitle { get; init; }
    public string EpisodeDescription { get; init; }
    public DateTime Release { get; init; }
    public TimeSpan Duration { get; init; }
    public Uri? Url { get; init; }
    public bool Explicit { get; init; }
    public Uri? Image { get; init; }
}
