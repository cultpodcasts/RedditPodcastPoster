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
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly IAppleUrlCategoriser _appleUrlCategoriser;
    private readonly ILogger<EnrichPodcastEpisodesProcessor> _logger;
    private readonly IPodcastRepository _podcastsRepository;
    private readonly ISpotifyEpisodeResolver _spotifyEpisodeResolver;
    private readonly ISpotifyUrlCategoriser _spotifyUrlCategoriser;
    private readonly IYouTubeUrlCategoriser _youTubeUrlCategoriser;

    public EnrichPodcastEpisodesProcessor(
        IPodcastRepository podcastsRepository,
        ISpotifyUrlCategoriser spotifyUrlCategoriser,
        IAppleUrlCategoriser appleUrlCategoriser,
        IYouTubeUrlCategoriser youTubeUrlCategoriser,
        ISpotifyEpisodeResolver spotifyEpisodeResolver,
        IAppleEpisodeResolver appleEpisodeResolver,
        ILogger<EnrichPodcastEpisodesProcessor> logger)
    {
        _podcastsRepository = podcastsRepository;
        _spotifyUrlCategoriser = spotifyUrlCategoriser;
        _appleUrlCategoriser = appleUrlCategoriser;
        _youTubeUrlCategoriser = youTubeUrlCategoriser;
        _spotifyEpisodeResolver = spotifyEpisodeResolver;
        _appleEpisodeResolver = appleEpisodeResolver;
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
                else
                {
                    if ((!string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null) &&
                        podcast.ReleaseAuthority == Service.YouTube)
                    {
                        var spotifyEpisode =
                            await _spotifyEpisodeResolver.FindEpisode(
                                FindSpotifyEpisodeRequestFactory.Create(podcast, episode), indexingContext);
                        if (spotifyEpisode.FullEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty,
                                podcast.Publisher, spotifyEpisode.FullEpisode.Name,
                                spotifyEpisode.FullEpisode.Description, spotifyEpisode.FullEpisode.GetReleaseDate(),
                                spotifyEpisode.FullEpisode.GetDuration());
                            match = await _appleUrlCategoriser.Resolve(refinedCriteria, podcast, indexingContext);
                            if (match != null)
                            {
                                episode.Urls.Apple ??= match.Url;
                                episode.AppleId ??= match.EpisodeId;
                                _logger.LogInformation(
                                    $"Enriched from apple: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                                updated = true;
                            }
                        }
                    }
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
                        episode.SpotifyId = match.EpisodeId;
                    }

                    _logger.LogInformation($"Enriched from spotify: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                    updated = true;
                }
                else
                {
                    if ((episode.AppleId != null || episode.Urls.Apple != null) &&
                        podcast.ReleaseAuthority == Service.YouTube)
                    {
                        var appleEpisode =
                            await _appleEpisodeResolver.FindEpisode(
                                FindAppleEpisodeRequestFactory.Create(podcast, episode), indexingContext);
                        if (appleEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty,
                                podcast.Publisher, appleEpisode.Title, appleEpisode.Description, appleEpisode.Release,
                                appleEpisode.Duration);
                            match = await _spotifyUrlCategoriser.Resolve(refinedCriteria, podcast, indexingContext);
                            if (match != null)
                            {
                                episode.Urls.Spotify ??= match.Url;
                                episode.SpotifyId = match.EpisodeId;
                                _logger.LogInformation(
                                    $"Enriched from spotify: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                                updated = true;
                            }
                        }
                    }
                }
            }
        }

        if (updated)
        {
            podcast.Episodes = podcast.Episodes.OrderByDescending(x => x.Release).ToList();
            await _podcastsRepository.Save(podcast);
        }
    }
}