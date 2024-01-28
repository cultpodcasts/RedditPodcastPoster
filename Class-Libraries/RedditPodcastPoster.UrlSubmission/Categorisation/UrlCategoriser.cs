using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public class UrlCategoriser(
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    ILogger<UrlCategoriser> logger)
    : IUrlCategoriser
{
    public async Task<CategorisedItem> Categorise(
        Podcast? podcast,
        Uri url,
        IndexingContext indexingContext,
        bool matchOtherServices)
    {
        ResolvedSpotifyItem? resolvedSpotifyItem = null;
        ResolvedAppleItem? resolvedAppleItem = null;
        ResolvedYouTubeItem? resolvedYouTubeItem = null;
        PodcastServiceSearchCriteria? criteria = null;
        Service authority = 0;

        Episode? matchingEpisode = null;

        if (spotifyUrlCategoriser.IsMatch(url))
        {
            resolvedSpotifyItem = await spotifyUrlCategoriser.Resolve(podcast, url, indexingContext);
            matchingEpisode = podcast?.Episodes.SingleOrDefault(x =>
                x.Urls.Spotify == url || x.SpotifyId == resolvedSpotifyItem.EpisodeId);
            criteria = resolvedSpotifyItem.ToPodcastServiceSearchCriteria();
            authority = Service.Spotify;
        }
        else if (appleUrlCategoriser.IsMatch(url))
        {
            resolvedAppleItem = await appleUrlCategoriser.Resolve(podcast, url, indexingContext);
            criteria = resolvedAppleItem.ToPodcastServiceSearchCriteria();
            matchingEpisode =
                podcast?.Episodes.SingleOrDefault(x => x.Urls.Apple == url || x.AppleId == resolvedAppleItem.EpisodeId);
            authority = Service.Apple;
        }
        else if (youTubeUrlCategoriser.IsMatch(url))
        {
            resolvedYouTubeItem = await youTubeUrlCategoriser.Resolve(podcast, url, indexingContext);
            if (resolvedYouTubeItem != null)
            {
                criteria = resolvedYouTubeItem.ToPodcastServiceSearchCriteria();
                matchingEpisode = podcast?.Episodes.SingleOrDefault(x =>
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
                if (resolvedSpotifyItem == null && !spotifyUrlCategoriser.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.SpotifyId) ||
                     matchingEpisode?.Urls.Spotify == null ||
                     string.IsNullOrWhiteSpace(podcast?.SpotifyId)))
                {
                    if (authority == Service.Apple)
                    {
                        indexingContext = indexingContext with {ReleasedSince = criteria.Release.AddDays(-1)};
                    }
                    else if (authority == Service.YouTube && podcast != null)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.Subtract(podcast.YouTubePublishingDelay())
                        };
                    }

                    resolvedSpotifyItem =
                        await spotifyUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                    if (resolvedSpotifyItem != null)
                    {
                        criteria = criteria.Merge(resolvedSpotifyItem);
                    }
                }


                if (resolvedAppleItem == null && !appleUrlCategoriser.IsMatch(url) &&
                    (matchingEpisode?.AppleId == null ||
                     matchingEpisode?.Urls.Apple == null ||
                     podcast?.AppleId == null))
                {
                    if (authority == Service.Spotify)
                    {
                        indexingContext = indexingContext with {ReleasedSince = criteria.Release.AddDays(-1)};
                    }
                    else if (authority == Service.YouTube && podcast != null)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.Subtract(podcast.YouTubePublishingDelay())
                        };
                    }

                    resolvedAppleItem = await appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                    if (resolvedAppleItem != null)
                    {
                        criteria = criteria.Merge(resolvedAppleItem);
                    }
                }

                if (resolvedYouTubeItem == null && !youTubeUrlCategoriser.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.YouTubeId) ||
                     matchingEpisode?.Urls.YouTube == null ||
                     string.IsNullOrWhiteSpace(podcast?.YouTubeChannelId)))
                {
                    if ((authority == Service.Spotify || authority == Service.Apple) && podcast != null)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.Add(podcast.YouTubePublishingDelay())
                        };
                    }

                    resolvedYouTubeItem =
                        await youTubeUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                    if (resolvedYouTubeItem != null)
                    {
                        criteria = criteria.Merge(resolvedYouTubeItem);
                    }
                }
            }

            return new CategorisedItem(
                podcast,
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