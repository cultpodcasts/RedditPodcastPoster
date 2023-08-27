using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.YouTube;

namespace EnrichYouTubeOnlyPodcasts;

public class EnrichYouTubePodcastProcessor
{
    private readonly ILogger<EnrichYouTubePodcastProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IYouTubeEpisodeProvider _youTubeEpisodeProvider;
    private readonly IYouTubeSearchService _youTubeSearchService;

    public EnrichYouTubePodcastProcessor(
        IPodcastRepository podcastRepository,
        IYouTubeSearchService youTubeSearchService,
        IYouTubeEpisodeProvider youTubeEpisodeProvider,
        ILogger<EnrichYouTubePodcastProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _youTubeSearchService = youTubeSearchService;
        _youTubeEpisodeProvider = youTubeEpisodeProvider;
        _logger = logger;
    }

    public async Task Run(EnrichYouTubePodcastRequest request)
    {
        var podcasts = await _podcastRepository.GetAll().ToListAsync();
        var podcast = podcasts.Single(x => x.Id == request.PodcastGuid);
        if (string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            throw new InvalidOperationException(
                "Not appropriate to run this app against a podcast without a YouTube channel-id");
        }
        Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, RegexOptions.Compiled);
        }

        var playlistItems = await _youTubeSearchService.GetPlaylist(request.playlistId);

        var missingPlaylistItems = playlistItems.Where(playlistItem =>
            podcast.Episodes.All(episode =>
            {
                if (episode.Title.Trim() == playlistItem.Snippet.Title.Trim())
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(episode.YouTubeId) &&
                    episode.YouTubeId == playlistItem.Snippet.ResourceId.VideoId)
                {
                    return false;
                }

                if (episodeMatchRegex != null)
                {
                    var playlistItemMatch = episodeMatchRegex.Match(playlistItem.Snippet.Title);
                    var episodeMatch = episodeMatchRegex.Match(episode.Title);
                    if (playlistItemMatch.Success && episodeMatch.Success)
                    {
                        return playlistItemMatch.Groups["episodematch"].Value !=
                               episodeMatch.Groups["episodematch"].Value;
                    }
                }

                return true;
            })).ToList();
        var missingVideoIds = missingPlaylistItems.Select(x => x.Snippet.ResourceId.VideoId).Distinct();
        var missingPlaylistVideos =
            await _youTubeSearchService.GetVideoDetails(
                missingVideoIds);

        foreach (var missingPlaylistItem in missingPlaylistItems)
        {
            var video = missingPlaylistVideos.Single(video =>
                video.Id == missingPlaylistItem.Snippet.ResourceId.VideoId);

            var episode = _youTubeEpisodeProvider.GetEpisode(missingPlaylistItem.Snippet, video);
            episode.Id = Guid.NewGuid();
            podcast.Episodes.Add(episode);
        }

        podcast.Episodes = podcast.Episodes.OrderByDescending(x => x.Release).ToList();

        await _podcastRepository.Save(podcast);
    }
}