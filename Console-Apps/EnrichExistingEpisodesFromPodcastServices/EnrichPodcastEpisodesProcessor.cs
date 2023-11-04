using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace EnrichExistingEpisodesFromPodcastServices;

public class EnrichPodcastEpisodesProcessor
{
    private readonly IAppleUrlCategoriser _appleUrlCategoriser;
    private readonly ILogger<EnrichPodcastEpisodesProcessor> _logger;
    private readonly IPodcastRepository _podcastsRepository;
    private readonly ISpotifyUrlCategoriser _spotifyUrlCategoriser;
    private readonly IYouTubeUrlCategoriser _youTubeUrlCategoriser;

    public EnrichPodcastEpisodesProcessor(
        IPodcastRepository podcastsRepository,
        ISpotifyUrlCategoriser spotifyUrlCategoriser,
        IAppleUrlCategoriser appleUrlCategoriser,
        IYouTubeUrlCategoriser youTubeUrlCategoriser,
        ILogger<EnrichPodcastEpisodesProcessor> logger)
    {
        _podcastsRepository = podcastsRepository;
        _spotifyUrlCategoriser = spotifyUrlCategoriser;
        _appleUrlCategoriser = appleUrlCategoriser;
        _youTubeUrlCategoriser = youTubeUrlCategoriser;
        _logger = logger;
    }

    public async Task Run(EnrichPodcastEpisodesRequest request)
    {
        IndexingContext indexingContext;
        if (request.ReleasedSince.HasValue)
        {
            indexingContext = new IndexingContext(DateTimeHelper.DaysAgo(request.ReleasedSince.Value));
        }
        else
        {
            indexingContext = new IndexingContext();
        }

        indexingContext.SkipExpensiveSpotifyQueries = !request.AllowExpensiveQueries;
        indexingContext.SkipExpensiveYouTubeQueries = !request.AllowExpensiveQueries;
        indexingContext.SkipYouTubeUrlResolving = request.SkipYouTubeUrlResolving;

        var podcast = await _podcastsRepository.GetPodcast(request.PodcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast found with id '{request.PodcastId}'.");
        }

        IEnumerable<Episode> episodes = podcast.Episodes;
        if (request.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince);
        }

        var updated = false;
        foreach (var episode in episodes)
        {
            var criteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty, podcast.Publisher,
                episode.Title, episode.Description, episode.Release, episode.Length);
            if (podcast.AppleId != null && (episode.AppleId == null || episode.Urls.Apple == null))
            {
                var match = await _appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Apple ??= match.Url;
                    episode.AppleId ??= match.EpisodeId;
                    _logger.LogInformation($"Enriched from apple: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                    updated = true;
                }
            }

            if (podcast.YouTubeChannelId != null &&
                (string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube == null))
            {
                var match = await _youTubeUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.YouTube ??= match.Url;
                    if (string.IsNullOrWhiteSpace(episode.YouTubeId))
                    {
                        episode.YouTubeId = match.EpisodeId;
                    }
                    _logger.LogInformation($"Enriched from youtube: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                    updated = true;
                }
            }

            if (podcast.SpotifyId != null &&
                (string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify == null))
            {
                var match = await _spotifyUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Spotify ??= match.Url;
                    if (string.IsNullOrWhiteSpace(episode.SpotifyId))
                    {
                        episode.SpotifyId= match.EpisodeId;
                    }
                    _logger.LogInformation($"Enriched from spotify: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                    updated = true;
                }
            }
        }

        if (updated)
        {
            await _podcastsRepository.Save(podcast);
        }
    }
}