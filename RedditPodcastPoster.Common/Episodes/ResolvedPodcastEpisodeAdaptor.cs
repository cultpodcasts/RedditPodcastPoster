using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Models;
using System.Globalization;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class ResolvedPodcastEpisodeAdaptor : IResolvedPodcastEpisodeAdaptor
{
    private readonly ILogger<ResolvedPodcastEpisodeAdaptor> _logger;

    public ResolvedPodcastEpisodeAdaptor(ILogger<ResolvedPodcastEpisodeAdaptor> logger)
    {
        _logger = logger;
    }

    public PostModel ToPostModel(Podcast podcast, IEnumerable<Episode> episodes)
    {
        var postModel = new PostModel(new PodcastPost(
            podcast.Name,
            podcast.TitleRegex,
            podcast.DescriptionRegex,
            episodes.Select(ToBasicEpisode),
            podcast.PrimaryPostService
        ));
        return postModel;
    }

    private EpisodePost ToBasicEpisode(Episode episode)
    {
        var id = "unknown";
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId))
        {
            id = $"Spotify-{episode.SpotifyId}";
        }
        else if (episode.AppleId!=null)
        {
            id = $"Apple-{episode.AppleId}";
        }
        else if (!string.IsNullOrWhiteSpace(episode.YouTubeId))
        {
            id = $"YouTube-{episode.YouTubeId}";
        }

        return new EpisodePost(
            episode.Title, 
            episode.Urls.YouTube, 
            episode.Urls.Spotify, 
            episode.Urls.Apple,
            episode.Release.ToString("dd MMM yyyy"), 
            episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture), 
            episode.Description, 
            id);
    }
}