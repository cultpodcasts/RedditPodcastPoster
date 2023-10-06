using System.Text.RegularExpressions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

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
        var indexOptions = new IndexingContext {ReleasedSince = DateTime.Today.AddDays(-1 * request.ReleasedSince)};
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

        string playlistId;
        if (string.IsNullOrWhiteSpace(request.PlaylistId))
        {
            var channel =
                await _youTubeSearchService.GetChannelContentDetails(new YouTubeChannelId(podcast.YouTubeChannelId),
                    indexOptions, false, false);
            if (channel == null)
            {
                throw new InvalidOperationException(
                    $"Could not find YouTube channel with channel-id '{podcast.YouTubeChannelId}'.");
            }

            playlistId = channel.ContentDetails.RelatedPlaylists.Uploads;
        }
        else
        {
            playlistId = request.PlaylistId;
        }

        var playlistItems =
            await _youTubeSearchService.GetPlaylistVideoSnippets(new YouTubePlaylistId(playlistId),
                indexOptions);

        var missingPlaylistItems = playlistItems.Where(playlistItem =>
            podcast.Episodes.All(episode => !Matches(episode, playlistItem, episodeMatchRegex))).ToList();
        var missingVideoIds = missingPlaylistItems.Select(x => x.Snippet.ResourceId.VideoId).Distinct();
        var missingPlaylistVideos = await _youTubeSearchService.GetVideoContentDetails(missingVideoIds, indexOptions);

        foreach (var missingPlaylistItem in missingPlaylistItems)
        {
            var missingPlaylistItemSnippet = playlistItems.SingleOrDefault(x => x.Id == missingPlaylistItem.Id).Snippet;
            var video = missingPlaylistVideos.SingleOrDefault(video =>
                video.Id == missingPlaylistItemSnippet.ResourceId.VideoId);
            if (video != null)
            {
                var episode = _youTubeEpisodeProvider.GetEpisode(missingPlaylistItemSnippet, video);
                episode.Id = Guid.NewGuid();
                podcast.Episodes.Add(episode);
            }
        }

        foreach (var podcastEpisode in podcast.Episodes)
        {
            if (string.IsNullOrWhiteSpace(podcastEpisode.YouTubeId) || podcastEpisode.Urls.YouTube == null)
            {
                var youTubeItems = playlistItems.Where(x => Matches(podcastEpisode, x, episodeMatchRegex));

                var youTubeItem = youTubeItems.FirstOrDefault();
                if (youTubeItem != null)
                {
                    podcastEpisode.YouTubeId = youTubeItem.Snippet.ResourceId.VideoId;
                    podcastEpisode.Urls.YouTube = youTubeItem.Snippet.ToYouTubeUrl();
                }
            }
        }

        podcast.Episodes = podcast.Episodes.OrderByDescending(x => x.Release).ToList();
        await _podcastRepository.Save(podcast);
    }

    private static bool Matches(Episode episode, PlaylistItem playlistItem, Regex? episodeMatchRegex)
    {
        if (episode.Title.Trim() == playlistItem.Snippet.Title.Trim())
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(episode.YouTubeId) &&
            episode.YouTubeId == playlistItem.Snippet.ResourceId.VideoId)
        {
            return true;
        }

        if (episodeMatchRegex != null)
        {
            var playlistItemMatch = episodeMatchRegex.Match(playlistItem.Snippet.Title);
            var episodeMatch = episodeMatchRegex.Match(episode.Title);
            if (playlistItemMatch.Success && episodeMatch.Success)
            {
                if (playlistItemMatch.Groups["episodematch"].Value ==
                    episodeMatch.Groups["episodematch"].Value)
                {
                    return true;
                }

                if (playlistItemMatch.Groups["title"].Value ==
                    episodeMatch.Groups["title"].Value)
                {
                    return true;
                }
            }
        }

        return false;
    }
}