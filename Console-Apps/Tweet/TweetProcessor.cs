using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.UrlShortening;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace Tweet;

public class TweetProcessor(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ITweetBuilder tweetBuilder,
    ITwitterClient twitterClient,
    IShortnerService shortnerService,
    ILogger<TweetProcessor> logger)
{
    public async Task Run(TweetRequest request)
    {
        var podcast = await podcastRepository.GetPodcast(request.PodcastId);
        if (podcast != null)
        {
            var podcastEpisodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();
            var mostRecentEpisode =
                podcastEpisodes
                    .Where(x => x is { Tweeted: false, Ignored: false, Removed: false })
                    .MaxBy(x => x.Release);

            if (mostRecentEpisode != null)
            {
                var podcastEpisode = CreatePodcastEpisode(podcast, mostRecentEpisode, podcastEpisodes);
                var shortnerResult = await shortnerService.Write(podcastEpisode);
                if (!shortnerResult.Success)
                {
                    logger.LogError("Unsuccessful shortening-url.");
                }

                var tweet = await tweetBuilder.BuildTweet(podcastEpisode, shortnerResult.Url);
                var tweetStatus = await twitterClient.Send(tweet);
                var tweeted = tweetStatus.TweetSendStatus == TweetSendStatus.Sent;

                if (tweeted)
                {
                    mostRecentEpisode.Tweeted = true;
                    try
                    {
                        await episodeRepository.Save(mostRecentEpisode);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Failure to save episode with id '{EpisodeId}' for podcast-id '{PodcastId}'.",
                            mostRecentEpisode.Id, podcast.Id);
                        throw;
                    }
                }
                else
                {
                    var message =
                        $"Could not post tweet for podcast-episode: Podcast-id: '{podcastEpisode.Podcast.Id}', Episode-id: '{podcastEpisode.Episode.Id}'. Tweet: '{tweet}'.";
                    logger.LogError(message);
                    throw new Exception(message);
                }
            }
            else
            {
                var message =
                    $"Could not find an episode for podcast '{podcast.Name}' with id: '{podcast.Id}'.";
                logger.LogError(message);
                throw new Exception(message);
            }
        }
        else
        {
            var message =
                $"Could not find an podcast with id: '{request.PodcastId}'.";
            logger.LogError(message);
            throw new Exception(message);
        }
    }

    private static PodcastEpisode CreatePodcastEpisode(V2Podcast podcast, V2Episode episode, IEnumerable<V2Episode> allEpisodes)
    {
        var servicePodcast = new Podcast(podcast.Id)
        {
            Name = podcast.Name,
            Language = podcast.Language,
            Removed = podcast.Removed,
            Publisher = podcast.Publisher,
            Bundles = podcast.Bundles,
            IndexAllEpisodes = podcast.IndexAllEpisodes,
            IgnoreAllEpisodes = podcast.IgnoreAllEpisodes,
            BypassShortEpisodeChecking = podcast.BypassShortEpisodeChecking,
            MinimumDuration = podcast.MinimumDuration,
            ReleaseAuthority = podcast.ReleaseAuthority,
            PrimaryPostService = podcast.PrimaryPostService,
            SpotifyId = podcast.SpotifyId,
            SpotifyMarket = podcast.SpotifyMarket,
            SpotifyEpisodesQueryIsExpensive = podcast.SpotifyEpisodesQueryIsExpensive,
            AppleId = podcast.AppleId,
            YouTubeChannelId = podcast.YouTubeChannelId,
            YouTubePlaylistId = podcast.YouTubePlaylistId,
            YouTubePublicationOffset = podcast.YouTubePublicationOffset,
            YouTubePlaylistQueryIsExpensive = podcast.YouTubePlaylistQueryIsExpensive,
            SkipEnrichingFromYouTube = podcast.SkipEnrichingFromYouTube,
            YouTubeNotificationSubscriptionLeaseExpiry = podcast.YouTubeNotificationSubscriptionLeaseExpiry,
            TwitterHandle = podcast.TwitterHandle,
            BlueskyHandle = podcast.BlueskyHandle,
            HashTag = podcast.HashTag,
            EnrichmentHashTags = podcast.EnrichmentHashTags,
            TitleRegex = podcast.TitleRegex,
            DescriptionRegex = podcast.DescriptionRegex,
            EpisodeMatchRegex = podcast.EpisodeMatchRegex,
            EpisodeIncludeTitleRegex = podcast.EpisodeIncludeTitleRegex,
            IgnoredAssociatedSubjects = podcast.IgnoredAssociatedSubjects,
            IgnoredSubjects = podcast.IgnoredSubjects,
            DefaultSubject = podcast.DefaultSubject,
            SearchTerms = podcast.SearchTerms,
            KnownTerms = podcast.KnownTerms,
            FileKey = podcast.FileKey,
            Timestamp = podcast.Timestamp,
            Episodes = allEpisodes.Select(ToLegacyEpisode).ToList()
        };

        return new PodcastEpisode(servicePodcast, ToLegacyEpisode(episode));
    }

    private static Episode ToLegacyEpisode(V2Episode episode)
    {
        return new Episode
        {
            Id = episode.Id,
            PodcastId = episode.PodcastId,
            PodcastName = episode.PodcastName,
            PodcastSearchTerms = episode.PodcastSearchTerms,
            SearchLanguage = episode.SearchLanguage,
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
            Subjects = episode.Subjects,
            SearchTerms = episode.SearchTerms,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}