using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Services;

namespace RedditPodcastPoster.UrlSubmission.Categorisation;

public class UrlCategoriser(
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    INonPodcastServiceCategoriser nonPodcastServiceCategoriser,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<UrlCategoriser> logger)
#pragma warning restore CS9113 // Parameter is unread.
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
        ResolvedNonPodcastServiceItem? resolvedNonPodcastServiceItem = null;
        PodcastServiceSearchCriteria? criteria = null;
        Service authority = 0;

        Episode? matchingEpisode = null;

        if (SpotifyPodcastServiceMatcher.IsMatch(url))
        {
            resolvedSpotifyItem = await spotifyUrlCategoriser.Resolve(podcast, url, indexingContext);
            matchingEpisode = podcast?.Episodes.SingleOrDefault(x =>
                x.Urls.Spotify == url || x.SpotifyId == resolvedSpotifyItem.EpisodeId);
            criteria = resolvedSpotifyItem.ToPodcastServiceSearchCriteria();
            authority = Service.Spotify;
        }
        else if (ApplePodcastServiceMatcher.IsMatch(url))
        {
            resolvedAppleItem = await appleUrlCategoriser.Resolve(podcast, url, indexingContext);
            criteria = resolvedAppleItem.ToPodcastServiceSearchCriteria();
            matchingEpisode =
                podcast?.Episodes.SingleOrDefault(x => x.Urls.Apple == url || x.AppleId == resolvedAppleItem.EpisodeId);
            authority = Service.Apple;
        }
        else if (YouTubePodcastServiceMatcher.IsMatch(url))
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
        else if (NonPodcastServiceMatcher.MatchesBBC(url) || NonPodcastServiceMatcher.MatchesInternetArchive(url))
        {
            resolvedNonPodcastServiceItem = await nonPodcastServiceCategoriser.Resolve(podcast, url, indexingContext);
        }
        else
        {
            throw new InvalidOperationException($"Could not match url '{url}' to a service.");
        }

        if (criteria != null)
        {
            if (matchOtherServices)
            {
                if (resolvedSpotifyItem == null && !SpotifyPodcastServiceMatcher.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.SpotifyId) ||
                     // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
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


                if (resolvedAppleItem == null && !ApplePodcastServiceMatcher.IsMatch(url) &&
                    (matchingEpisode?.AppleId == null ||
                     // ReSharper disable once ConstantConditionalAccessQualifier
                     matchingEpisode?.Urls.Apple == null ||
                     podcast?.AppleId == null))
                {
                    if (authority == Service.Spotify)
                    {
                        indexingContext = indexingContext with
                        {
                            ReleasedSince = criteria.Release.AddDays(-1)
                        };
                        if (resolvedSpotifyItem != null)
                        {
                            criteria = criteria with
                            {
                                // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
                                Publisher = resolvedSpotifyItem.Publisher ?? string.Empty
                            };
                        }
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

                if (resolvedYouTubeItem == null && !YouTubePodcastServiceMatcher.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.YouTubeId) ||
                     // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                     matchingEpisode?.Urls.YouTube == null ||
                     string.IsNullOrWhiteSpace(podcast?.YouTubeChannelId)))
                {
                    if (authority is Service.Spotify or Service.Apple && podcast != null)
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
                resolvedNonPodcastServiceItem,
                authority);
        }

        if (resolvedNonPodcastServiceItem != null)
        {
            return new CategorisedItem(
                resolvedNonPodcastServiceItem.Podcast,
                resolvedNonPodcastServiceItem.Episode,
                null,
                null,
                null,
                resolvedNonPodcastServiceItem,
                Service.Other);
        }

        if (!indexingContext.SkipYouTubeUrlResolving)
        {
            throw new InvalidOperationException($"Could not find episode with url '{url}'.");
        }

        throw new InvalidOperationException($"Unable to handle url '{url}'.");
    }
}