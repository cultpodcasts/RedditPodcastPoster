using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class ResolvedSpotifyItem
{
    public ResolvedSpotifyItem(PodcastEpisode podcastEpisode)
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

        Image = podcastEpisode.Episode.Images?.Spotify;
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

    public string ShowId { get; init; }
    public string EpisodeId { get; init; }
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