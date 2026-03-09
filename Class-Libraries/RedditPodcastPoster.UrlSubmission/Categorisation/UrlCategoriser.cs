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
using V2Episode = RedditPodcastPoster.Models.V2.Episode;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;

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
            var v2Podcast = podcast != null ? ToV2Podcast(podcast) : null;
            var v2Episodes = podcast?.Episodes.Select(e => ToV2Episode(podcast, e)) ?? Enumerable.Empty<V2Episode>();
            resolvedAppleItem = await appleUrlCategoriser.Resolve(v2Podcast, v2Episodes, url, indexingContext);
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
            if (resolvedNonPodcastServiceItem != null)
            {
                authority = Service.Other;
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
                if (resolvedSpotifyItem == null && !SpotifyPodcastServiceMatcher.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.SpotifyId) ||
                     matchingEpisode?.Urls.Spotify == null ||
                     string.IsNullOrWhiteSpace(podcast?.SpotifyId)))
                {
                    if (authority == Service.Apple)
                    {
                        indexingContext = indexingContext with { ReleasedSince = criteria.Release.AddDays(-1) };
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

                    resolvedAppleItem = await appleUrlCategoriser.Resolve(criteria, podcast != null ? ToV2Podcast(podcast) : null, indexingContext);
                    if (resolvedAppleItem != null)
                    {
                        criteria = criteria.Merge(resolvedAppleItem);
                    }
                }

                if (resolvedYouTubeItem == null && !YouTubePodcastServiceMatcher.IsMatch(url) &&
                    (string.IsNullOrWhiteSpace(matchingEpisode?.YouTubeId) ||
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

    private static V2Podcast ToV2Podcast(Podcast podcast)
    {
        return new V2Podcast
        {
            Id = podcast.Id,
            Name = podcast.Name,
            Publisher = podcast.Publisher,
            ReleaseAuthority = podcast.ReleaseAuthority,
            SpotifyId = podcast.SpotifyId,
            SpotifyMarket = podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = podcast.AppleId,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive
        };
    }

    private static V2Episode ToV2Episode(Podcast podcast, Episode episode)
    {
        return new V2Episode
        {
            Id = episode.Id,
            PodcastId = podcast.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Removed = episode.Removed,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = episode.Urls,
            Subjects = episode.Subjects ?? [],
            SearchTerms = episode.SearchTerms,
            SearchLanguage = episode.Language,
            PodcastName = podcast.Name,
            PodcastSearchTerms = podcast.SearchTerms,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}