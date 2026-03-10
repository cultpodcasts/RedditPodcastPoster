using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.PodcastServices.Spotify.Enrichers;

public class SpotifyPodcastEnricher(
    ISpotifyEpisodeResolver spotifyIdResolver,
    ISpotifyPodcastResolver spotifyPodcastResolver,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SpotifyPodcastEnricher> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : ISpotifyPodcastEnricher
{
    public async Task<bool> AddIdAndUrls(Podcast podcast, IndexingContext indexingContext)
    {
        var podcastShouldUpdate = false;
        if (string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var matchedPodcast =
                await spotifyPodcastResolver.FindPodcast(podcast.ToFindSpotifyPodcastRequest(), indexingContext);
            if (matchedPodcast != null)
            {
                if (!string.IsNullOrWhiteSpace(matchedPodcast.Id))
                {
                    podcast.SpotifyId = matchedPodcast.Id;
                    podcastShouldUpdate = true;
                }

                if (matchedPodcast.ExpensiveQueryFound)
                {
                    podcast.SpotifyEpisodesQueryIsExpensive = true;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            var podcastV2 = podcast.ToV2Podcast();
            foreach (var podcastEpisode in podcast.Episodes)
            {
                if (string.IsNullOrWhiteSpace(podcastEpisode.SpotifyId))
                {
                    var findEpisodeResponse = await spotifyIdResolver.FindEpisode(
                        FindSpotifyEpisodeRequestFactory.Create(
                            podcastV2,
                            ToV2Episode(podcastV2, podcastEpisode)),
                        indexingContext);
                    if (!string.IsNullOrWhiteSpace(findEpisodeResponse.FullEpisode?.Id))
                    {
                        podcastEpisode.SpotifyId = findEpisodeResponse.FullEpisode.Id;
                        podcastShouldUpdate = true;
                    }

                    if (findEpisodeResponse.IsExpensiveQuery)
                    {
                        podcast.SpotifyEpisodesQueryIsExpensive = true;
                    }
                }
            }
        }

        return podcastShouldUpdate;
    }

    private static V2Episode ToV2Episode(V2Podcast podcast, Episode episode)
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
            PodcastName = podcast.Name,
            PodcastSearchTerms = podcast.SearchTerms,
            Language = episode.Language ?? podcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}