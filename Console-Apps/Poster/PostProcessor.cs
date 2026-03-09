using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.UrlShortening;
using PodcastEpisode = RedditPodcastPoster.Models.PodcastEpisode;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace Poster;

public class PostProcessor(
    IPodcastRepositoryV2 repository,
    IEpisodeRepository episodeRepository,
    ITweeter tweeter,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    IContentPublisher contentPublisher,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IBlueskyPoster blueSkyPoster,
    IBlueskyPostManager blueskyPostManager,
    IShortnerService shortnerService,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    ILogger<PostProcessor> logger)
{
    public async Task Process(PostRequest request)
    {
        if (!request.SkipReddit)
        {
            await Post(request);
        }

        if (!request.SkipPublish)
        {
            await Publish();
        }

        if (!request.SkipTweet || !request.SkipBluesky)
        {
            await PostToSocial(request);
        }
    }

    private async Task Post(PostRequest request)
    {
        IList<Guid> podcastIds;

        if (request.EpisodeId.HasValue)
        {
            var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId.Value);
            if (episode == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            var podcastId = await repository.GetBy(x => x.Removed != true && x.Id == episode.PodcastId);
            if (podcastId == null)
            {
                throw new ArgumentException($"Podcast with id '{episode.PodcastId}' not found for episode-id '{request.EpisodeId.Value}'.");
            }

            podcastIds = [podcastId.Id];
        }
        else if (request.PodcastId.HasValue)
        {
            var podcast = await repository.GetBy(x => x.Removed != true && x.Id == request.PodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId.Value}' not found.");
            }

            podcastIds = [request.PodcastId.Value];
        }
        else if (request.PodcastName != null)
        {
            var ids = await repository.GetAllBy(x =>
                    x.Removed != true &&
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Id)
                .ToListAsync();
            logger.LogInformation("Found {idsCount} podcasts.", ids.Count);
            podcastIds = ids.ToArray();
        }
        else
        {
            var since = DateTimeExtensions.DaysAgo(7);
            var ids = await episodeRepository.GetAllBy(x =>
                    x.Release >= since &&
                    !x.Posted &&
                    !x.Ignored &&
                    !x.Removed &&
                    (x.PodcastRemoved == null || x.PodcastRemoved == false))
                .Select(x => x.PodcastId)
                .Distinct()
                .ToListAsync();
            podcastIds = ids;
        }

        await PostNewEpisodes(request, podcastIds);
    }

    private async Task Publish()
    {
        Task[] publishingTasks = [contentPublisher.PublishHomepage()];
        await Task.WhenAll(publishingTasks);
    }

    private async Task PostToSocial(PostRequest request)
    {
        if (request.EpisodeId.HasValue)
        {
            var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId.Value);
            if (episode == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            var selectedPodcast = await repository.GetBy(x => x.Removed != true && x.Id == episode.PodcastId);
            if (selectedPodcast == null)
            {
                throw new ArgumentException($"Podcast with id '{episode.PodcastId}' not found for episode-id '{request.EpisodeId.Value}'.");
            }

            var selectedEpisode = ToLegacyEpisode(episode);
            var podcastEpisode = new PodcastEpisode(ToLegacyPodcast(selectedPodcast), selectedEpisode);

            var shortnerResult = await shortnerService.Write(podcastEpisode);
            if (!shortnerResult.Success)
            {
                logger.LogError("Unsuccessful shortening-url.");
            }

            if (!request.SkipTweet)
            {
                await TweetEpisode(podcastEpisode, shortnerResult.Url);
            }

            if (!request.SkipBluesky)
            {
                await PostBluesky(podcastEpisode, shortnerResult.Url);
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.PodcastName) || request.PodcastId.HasValue)
            {
                Guid podcastId;
                if (!string.IsNullOrWhiteSpace(request.PodcastName))
                {
                    var podcasts = await repository.GetAllBy(x =>
                        x.Removed != true &&
                        x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase)
                    ).ToArrayAsync();
                    if (podcasts.Any())
                    {
                        if (podcasts.Length > 1)
                        {
                            podcasts = podcasts.Where(x => x.IndexAllEpisodes || x.EpisodeIncludeTitleRegex != "")
                                .ToArray();
                        }

                        if (!podcasts.Any())
                        {
                            throw new InvalidOperationException(
                                $"Podcast with name '{request.PodcastName}' not found that could have unposted episodes.");
                        }

                        podcastId = podcasts.First().Id;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Podcast with name '{request.PodcastName}' not found");
                    }
                }
                else
                {
                    podcastId = request.PodcastId!.Value;
                }

                if (!request.SkipTweet)
                {
                    var untweetedPodcastEpisodes =
                        await podcastEpisodeProvider.GetUntweetedPodcastEpisodes(podcastId);
                    var mostRecent = untweetedPodcastEpisodes.OrderByDescending(x => x.Episode.Release)
                        .FirstOrDefault();
                    if (mostRecent != null)
                    {
                        var shortnerResult = await shortnerService.Write(mostRecent);
                        if (!shortnerResult.Success)
                        {
                            logger.LogError("Unsuccessful shortening-url.");
                        }

                        await TweetEpisode(mostRecent, shortnerResult.Url);
                    }
                }

                if (!request.SkipBluesky)
                {
                    var unPostedPodcastEpisodes =
                        await podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes(podcastId);
                    var mostRecent = unPostedPodcastEpisodes.OrderByDescending(x => x.Episode.Release)
                        .FirstOrDefault();
                    if (mostRecent != null)
                    {
                        var shortnerResult = await shortnerService.Write(mostRecent);
                        if (!shortnerResult.Success)
                        {
                            logger.LogError("Unsuccessful shortening-url.");
                        }

                        await PostBluesky(mostRecent, shortnerResult.Url);
                    }
                }
            }
            else
            {
                if (!request.SkipTweet)
                {
                    await tweeter.Tweet(true, true);
                }

                if (!request.SkipBluesky)
                {
                    await blueskyPostManager.Post(true, true);
                }
            }
        }
    }

    private async Task PostBluesky(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var result = await blueSkyPoster.Post(podcastEpisode, shortUrl);
        if (result != BlueskySendStatus.Success)
        {
            logger.LogError("Error sending bluesky post. Reason: '{reason}'.", result);
        }
    }

    private async Task TweetEpisode(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var result = await tweetPoster.PostTweet(podcastEpisode, shortUrl);
        if (result.TweetSendStatus != TweetSendStatus.Sent)
        {
            switch (result.TweetSendStatus)
            {
                case TweetSendStatus.DuplicateForbidden:
                    logger.LogError("Forbidden to send duplicate-tweet");
                    break;
                case TweetSendStatus.TooManyRequests:
                    logger.LogError("Too many twitter requests");
                    break;
                case TweetSendStatus.Failed:
                    logger.LogError("Failed to send tweet.");
                    break;
                default:
                    logger.LogError("Unknown tweet-send response '{result}'.", result.ToString());
                    break;
            }
        }
    }

    private async Task PostNewEpisodes(PostRequest request, IList<Guid> podcastIds)
    {
        if (request.EpisodeId.HasValue)
        {
            var detachedEpisode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId.Value);
            if (detachedEpisode == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            var selectedPodcast = await repository.GetPodcast(podcastIds.Single());
            if (selectedPodcast == null)
            {
                throw new ArgumentException($"Podcast with id '{podcastIds.Single()}' not found.");
            }

            var selectedEpisode = ToLegacyEpisode(detachedEpisode);
            var selectedPodcastLegacy = ToLegacyPodcast(selectedPodcast);

            if (selectedEpisode.Ignored && request.FlipIgnored)
            {
                selectedEpisode.Ignored = false;
                detachedEpisode.Ignored = false;
            }

            if (selectedEpisode.Posted || selectedEpisode.Ignored || selectedEpisode.Removed)
            {
                logger.LogWarning(
                    "Not posting episode with id '{episodeId}'. Posted: '{posted}', Ignored: '{ignored}', Removed: '{removed}'.",
                    request.EpisodeId, selectedEpisode.Posted, selectedEpisode.Ignored, selectedEpisode.Removed);
            }
            else
            {
                var podcastEpisode = new PodcastEpisode(selectedPodcastLegacy, selectedEpisode);
                var result = await podcastEpisodePoster.PostPodcastEpisode(
                    podcastEpisode, request.YouTubePrimaryPostService);
                if (!result.Success)
                {
                    logger.LogError(result.ToString());
                }

                detachedEpisode.Posted = selectedEpisode.Posted;
                detachedEpisode.Ignored = selectedEpisode.Ignored;
                detachedEpisode.Removed = selectedEpisode.Removed;
                await episodeRepository.Save(detachedEpisode);
            }
        }
        else
        {
            var results =
                await podcastEpisodesPoster.PostNewEpisodes(
                    DateTimeExtensions.DaysAgo(request.ReleasedWithin),
                    podcastIds,
                    preferYouTube: request.YouTubePrimaryPostService,
                    ignoreAppleGracePeriod: request.IgnoreAppleGracePeriod);
            var result = processResponsesAdaptor.CreateResponse(results);
            var message = result.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                if (!result.Success)
                {
                    logger.LogError(message);
                }
                else
                {
                    logger.LogInformation(message);
                }
            }
        }
    }

    private static RedditPodcastPoster.Models.Podcast ToLegacyPodcast(RedditPodcastPoster.Models.V2.Podcast podcast)
    {
        return new RedditPodcastPoster.Models.Podcast(podcast.Id)
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
            Timestamp = podcast.Timestamp
        };
    }

    private static RedditPodcastPoster.Models.Episode ToLegacyEpisode(V2Episode episode)
    {
        return new RedditPodcastPoster.Models.Episode
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
            Language = episode.SearchLanguage,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}