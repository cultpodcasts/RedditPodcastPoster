namespace RedditPodcastPoster.Common.UrlCategorisation;

public class ResolvedSpotifyItem
{
    public ResolvedSpotifyItem(PodcastEpisodePair podcastEpisode)
    {
        ShowId = podcastEpisode.Podcast.SpotifyId;
        EpisodeId = podcastEpisode.Episode.SpotifyId;
        ShowName = podcastEpisode.Podcast.Name;
        Publisher = podcastEpisode.Podcast.Publisher;
        EpisodeTitle = podcastEpisode.Episode.Title;
        EpisodeDescription = podcastEpisode.Episode.Description;
        Release = podcastEpisode.Episode.Release;
        Duration = podcastEpisode.Episode.Length;
        Explicit = podcastEpisode.Episode.Explicit;
        if (podcastEpisode.Episode.Urls.Spotify != null)
        {
            Url = podcastEpisode.Episode.Urls.Spotify;
        }
    }

    public ResolvedSpotifyItem(
        string showId,
        string episodeId,
        string showName,
        string showDescription,
        string publisher,
        string episodeTitle,
        string episodeDescription,
        DateTime release,
        TimeSpan duration,
        Uri url,
        bool @explicit)
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
    }

    public string ShowId { get; init; }
    public string EpisodeId { get; init; }
    public string ShowName { get; init; }
    public string ShowDescription { get; init; } = string.Empty;
    public string Publisher { get; init; }
    public string EpisodeTitle { get; init; }
    public string EpisodeDescription { get; init; }
    public DateTime Release { get; init; }
    public TimeSpan Duration { get; init; }
    public Uri? Url { get; init; } = null;
    public bool Explicit { get; init; }
}