using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Models;

public class ResolvedYouTubeItem
{
    public ResolvedYouTubeItem(
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
        Uri? image,
        string? playlistId)
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
        PlaylistId = playlistId;
    }

    public ResolvedYouTubeItem(PodcastEpisode podcastEpisode)
    {
        ShowId = podcastEpisode.Podcast.YouTubeChannelId;
        EpisodeId = podcastEpisode.Episode.YouTubeId;
        ShowName = podcastEpisode.Podcast.Name;
        Publisher = podcastEpisode.Podcast.Publisher;
        EpisodeTitle = podcastEpisode.Episode.Title;
        EpisodeDescription = podcastEpisode.Episode.Description;
        Release = podcastEpisode.Episode.Release;
        Duration = podcastEpisode.Episode.Length;
        Explicit = podcastEpisode.Episode.Explicit;
        if (podcastEpisode.Episode.Urls.YouTube != null)
        {
            Url = podcastEpisode.Episode.Urls.YouTube;
        }

        Image = podcastEpisode.Episode.Images?.YouTube;
        PlaylistId = podcastEpisode.Podcast.YouTubePlaylistId;
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
    public string? PlaylistId { get; init; }
}