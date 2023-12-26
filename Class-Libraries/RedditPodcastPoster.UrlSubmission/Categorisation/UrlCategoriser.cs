using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public class UrlCategoriser : IUrlCategoriser
{
    private readonly IAppleUrlCategoriser _appleUrlCategoriser;
    private readonly ILogger<UrlCategoriser> _logger;
    private readonly ISpotifyUrlCategoriser _spotifyUrlCategoriser;
    private readonly IYouTubeUrlCategoriser _youTubeUrlCategoriser;

    public UrlCategoriser(
        ISpotifyUrlCategoriser spotifyUrlCategoriser,
        IAppleUrlCategoriser appleUrlCategoriser,
        IYouTubeUrlCategoriser youTubeUrlCategoriser,
        ILogger<UrlCategoriser> logger)
    {
        _spotifyUrlCategoriser = spotifyUrlCategoriser;
        _appleUrlCategoriser = appleUrlCategoriser;
        _youTubeUrlCategoriser = youTubeUrlCategoriser;
        _logger = logger;
    }

    public async Task<CategorisedItem> Categorise(
        IList<Podcast> podcasts,
        Uri url,
        IndexingContext indexingContext,
        bool searchForPodcast,
        bool matchOtherServices)
    {
        ResolvedSpotifyItem? resolvedSpotifyItem = null;
        ResolvedAppleItem? resolvedAppleItem = null;
        ResolvedYouTubeItem? resolvedYouTubeItem = null;
        PodcastServiceSearchCriteria? criteria = null;
        Service authority = 0;

        Podcast? matchingPodcast = null;
        Episode? matchingEpisode = null;


        if (_spotifyUrlCategoriser.IsMatch(url))
        {
            resolvedSpotifyItem = await _spotifyUrlCategoriser.Resolve(podcasts, url, indexingContext);
            if (searchForPodcast)
            {
                matchingPodcast = podcasts.SingleOrDefault(podcast => IsMatchingPodcast(podcast, resolvedSpotifyItem));
            }
            else
            {
                matchingPodcast = podcasts.Single();
            }

            matchingEpisode = matchingPodcast?.Episodes.SingleOrDefault(x =>
                x.Urls.Spotify == url || x.SpotifyId == resolvedSpotifyItem.EpisodeId);
            criteria = resolvedSpotifyItem.ToPodcastServiceSearchCriteria();
            authority = Service.Spotify;
        }
        else if (_appleUrlCategoriser.IsMatch(url))
        {
            resolvedAppleItem = await _appleUrlCategoriser.Resolve(podcasts, url, indexingContext);
            criteria = resolvedAppleItem.ToPodcastServiceSearchCriteria();
            if (searchForPodcast)
            {
                matchingPodcast = podcasts.SingleOrDefault(podcast => IsMatchingPodcast(podcast, resolvedAppleItem));
            }
            else
            {
                matchingPodcast = podcasts.Single();
            }

            matchingEpisode = matchingPodcast?.Episodes.SingleOrDefault(x =>
                x.Urls.Apple == url || x.AppleId == resolvedAppleItem.EpisodeId);
            authority = Service.Apple;
        }
        else if (_youTubeUrlCategoriser.IsMatch(url))
        {
            resolvedYouTubeItem = await _youTubeUrlCategoriser.Resolve(podcasts, url, indexingContext);
            if (resolvedYouTubeItem != null)
            {
                criteria = resolvedYouTubeItem.ToPodcastServiceSearchCriteria();

                if (searchForPodcast)
                {
                    var matchingPodcasts = podcasts.Where(podcast => IsMatchingPodcast(podcast, resolvedYouTubeItem));
                    if (matchingPodcasts.Count() == 1)
                    {
                        matchingPodcast = matchingPodcasts.Single();
                    }
                    else if (matchingPodcasts.Count() > 1)
                    {
                        matchingPodcast =
                            matchingPodcasts.SingleOrDefault(x => string.IsNullOrWhiteSpace(x.YouTubePlaylistId));
                    }
                }
                else
                {
                    matchingPodcast = podcasts.Single();
                }


                matchingEpisode = matchingPodcast?.Episodes.SingleOrDefault(x =>
                    x.Urls.YouTube == url || x.YouTubeId == resolvedYouTubeItem.EpisodeId);
                authority = Service.YouTube;
            }
        }
        else
        {
            throw new InvalidOperationException($"Could not match url '{url}' to a service.");
        }


        if (criteria != null)
        {
            if (matchOtherServices)
            {
                if (resolvedSpotifyItem == null && !_spotifyUrlCategoriser.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.SpotifyId) ||
                     matchingEpisode?.Urls.Spotify == null ||
                     string.IsNullOrWhiteSpace(matchingPodcast?.SpotifyId)))
                {
                    if (authority == Service.Apple)
                    {
                        indexingContext = indexingContext with {ReleasedSince = criteria.Release.AddDays(-1)};
                    }
                    else if (authority == Service.YouTube && matchingPodcast != null)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.Subtract(matchingPodcast.YouTubePublishingDelay())
                        };
                    }

                    resolvedSpotifyItem =
                        await _spotifyUrlCategoriser.Resolve(criteria, matchingPodcast, indexingContext);
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
                    if (authority == Service.Spotify)
                    {
                        indexingContext = indexingContext with {ReleasedSince = criteria.Release.AddDays(-1)};
                    }
                    else if (authority == Service.YouTube && matchingPodcast != null)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.Subtract(matchingPodcast.YouTubePublishingDelay())
                        };
                    }

                    resolvedAppleItem = await _appleUrlCategoriser.Resolve(criteria, matchingPodcast, indexingContext);
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
                    if ((authority == Service.Spotify || authority == Service.Apple) && matchingPodcast != null)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.Add(matchingPodcast.YouTubePublishingDelay())
                        };
                    }

                    resolvedYouTubeItem =
                        await _youTubeUrlCategoriser.Resolve(criteria, matchingPodcast, indexingContext);
                    if (resolvedYouTubeItem != null)
                    {
                        criteria = criteria.Merge(resolvedYouTubeItem);
                    }
                }
            }

            return new CategorisedItem(
                matchingPodcast,
                matchingEpisode,
                resolvedSpotifyItem,
                resolvedAppleItem,
                resolvedYouTubeItem,
                authority);
        }

        if (!indexingContext.SkipYouTubeUrlResolving)
        {
            throw new InvalidOperationException($"Could not find episode with url '{url}'.");
        }

        throw new InvalidOperationException($"Unable to handle url '{url}'.");
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