using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public class UrlCategoriser : IUrlCategoriser
{
    private readonly IAppleUrlCategoriser _appleUrlCategoriser;
    private readonly ILogger<UrlCategoriser> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ISpotifyUrlCategoriser _spotifyUrlCategoriser;
    private readonly IYouTubeUrlCategoriser _youTubeUrlCategoriser;

    public UrlCategoriser(
        IPodcastRepository podcastRepository,
        ISpotifyUrlCategoriser spotifyUrlCategoriser,
        IAppleUrlCategoriser appleUrlCategoriser,
        IYouTubeUrlCategoriser youTubeUrlCategoriser,
        ILogger<UrlCategoriser> logger)
    {
        _podcastRepository = podcastRepository;
        _spotifyUrlCategoriser = spotifyUrlCategoriser;
        _appleUrlCategoriser = appleUrlCategoriser;
        _youTubeUrlCategoriser = youTubeUrlCategoriser;
        _logger = logger;
    }

    public async Task<CategorisedItem> Categorise(Uri url)
    {
        ResolvedSpotifyItem? resolvedSpotifyItem = null;
        ResolvedAppleItem? resolvedAppleItem = null;
        ResolvedYouTubeItem? resolvedYouTubeItem = null;
        PodcastServiceSearchCriteria? criteria = null;

        var podcasts = await _podcastRepository.GetAll().ToListAsync();
        Podcast? matchingPodcast = null;
        Episode? matchingEpisode = null;


        if (_spotifyUrlCategoriser.IsMatch(url))
        {
            resolvedSpotifyItem = await _spotifyUrlCategoriser.Resolve(podcasts, url);
            matchingPodcast = podcasts.SingleOrDefault(podcast => IsMatchingPodcast(podcast, resolvedSpotifyItem));
            matchingEpisode = matchingPodcast?.Episodes.SingleOrDefault(x =>
                x.Urls.Spotify == url || x.SpotifyId == resolvedSpotifyItem.EpisodeId);
            criteria = resolvedSpotifyItem.ToPodcastServiceSearchCriteria();
        }
        else if (_appleUrlCategoriser.IsMatch(url))
        {
            resolvedAppleItem = await _appleUrlCategoriser.Resolve(podcasts, url);
            criteria = resolvedAppleItem.ToPodcastServiceSearchCriteria();
            matchingPodcast = podcasts.SingleOrDefault(podcast => IsMatchingPodcast(podcast, resolvedAppleItem));
            matchingEpisode = matchingPodcast?.Episodes.SingleOrDefault(x =>
                x.Urls.Apple == url || x.AppleId == resolvedAppleItem.EpisodeId);
        }
        else if (_youTubeUrlCategoriser.IsMatch(url))
        {
            resolvedYouTubeItem = await _youTubeUrlCategoriser.Resolve(podcasts, url);
            criteria = resolvedYouTubeItem.ToPodcastServiceSearchCriteria();
            matchingPodcast = podcasts.SingleOrDefault(podcast => IsMatchingPodcast(podcast, resolvedYouTubeItem));
            matchingEpisode = matchingPodcast?.Episodes.SingleOrDefault(x =>
                x.Urls.YouTube == url || x.YouTubeId == resolvedYouTubeItem.EpisodeId);
        }


        if (criteria != null)
        {
            if (resolvedSpotifyItem == null && !_spotifyUrlCategoriser.IsMatch(url) &&
                (string.IsNullOrWhiteSpace(matchingEpisode?.SpotifyId) ||
                 matchingEpisode?.Urls.Spotify == null ||
                 string.IsNullOrWhiteSpace(matchingPodcast?.SpotifyId)))
            {
                resolvedSpotifyItem = await _spotifyUrlCategoriser.Resolve(criteria, matchingPodcast);
                if (resolvedSpotifyItem != null)
                {
                    criteria = criteria.Merge(resolvedSpotifyItem);
                }
            }


            if (resolvedAppleItem == null && !_appleUrlCategoriser.IsMatch(url) &&
                (matchingEpisode?.AppleId == null ||
                 matchingEpisode?.Urls.Apple == null ||
                 matchingPodcast?.AppleId == null))
            {
                resolvedAppleItem = await _appleUrlCategoriser.Resolve(criteria, matchingPodcast);
                if (resolvedAppleItem != null)
                {
                    criteria = criteria.Merge(resolvedAppleItem);
                }
            }

            if (resolvedYouTubeItem == null && !_youTubeUrlCategoriser.IsMatch(url) &&
                (string.IsNullOrWhiteSpace(matchingEpisode?.YouTubeId) ||
                 matchingEpisode?.Urls.YouTube == null ||
                 string.IsNullOrWhiteSpace(matchingPodcast?.YouTubeChannelId)))
            {
                resolvedYouTubeItem = await _youTubeUrlCategoriser.Resolve(criteria, matchingPodcast);
                if (resolvedYouTubeItem != null)
                {
                    criteria = criteria.Merge(resolvedYouTubeItem);
                }
            }

            return new CategorisedItem(
                matchingPodcast,
                matchingEpisode,
                resolvedSpotifyItem,
                resolvedAppleItem,
                resolvedYouTubeItem);
        }

        throw new InvalidOperationException($"Could not find episode with url '{url}'.");
    }

    private bool IsMatchingPodcast(Podcast podcast, ResolvedSpotifyItem? resolvedItem)
    {
        if (resolvedItem != null &&
            !string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
            resolvedItem.ShowId == podcast.SpotifyId)
        {
            return true;
        }

        return false;
    }

    private bool IsMatchingPodcast(Podcast podcast, ResolvedAppleItem? resolvedItem)
    {
        if (resolvedItem != null &&
            podcast.AppleId.HasValue &&
            resolvedItem.ShowId.HasValue &&
            resolvedItem.ShowId == podcast.AppleId)
        {
            return true;
        }

        return false;
    }

    private bool IsMatchingPodcast(Podcast podcast, ResolvedYouTubeItem? resolvedItem)
    {
        if (resolvedItem != null &&
            !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
            resolvedItem.ShowId == podcast.YouTubeChannelId)
        {
            return true;
        }

        return false;
    }
}