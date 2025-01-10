using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Bluesky;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;
using RedditPodcastPoster.UrlShortening;

namespace Poster;

public class PostProcessor(
    IPodcastRepository repository,
    ITweeter tweeter,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    IContentPublisher contentPublisher,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    IBlueskyPoster blueSkyPoster,
    IBlueskyPostManager blueskyPostManager,
    IShortnerService shortnerService,
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
            var podcastId = await repository.GetBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Episodes.Any(ep => ep.Id == request.EpisodeId), x => new {guid = x.Id});
            if (podcastId == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            podcastIds = new[] {podcastId.guid};
        }
        else if (request.PodcastId.HasValue)
        {
            var podcast = await repository.GetBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Id == request.PodcastId.Value, x => new { });
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId.Value}' not found.");
            }

            podcastIds = new[] {request.PodcastId.Value};
        }
        else if (request.PodcastName != null)
        {
            var ids = await repository.GetAllBy(x =>
                    (!x.Removed.IsDefined() || x.Removed == false) &&
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                x => new {guid = x.Id}).ToListAsync();
            logger.LogInformation($"Found {ids.Count()} podcasts.");
            podcastIds = ids.Select(x => x.guid).ToArray();
        }
        else
        {
            var ids = await repository.GetPodcastsIdsWithUnpostedReleasedSince(
                DateTimeExtensions.DaysAgo(7));
            podcastIds = ids.ToList();
        }

        await PostNewEpisodes(request, podcastIds);
    }

    private async Task Publish()
    {
        Task[] publishingTasks =
        {
            contentPublisher.PublishHomepage()
        };

        await Task.WhenAll(publishingTasks);
    }

    private async Task PostToSocial(PostRequest request)
    {
        if (request.EpisodeId.HasValue)
        {
            var podcastId = await repository.GetBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Episodes.Any(ep => ep.Id == request.EpisodeId), x => new {guid = x.Id});
            if (podcastId == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            var selectedPodcast = await repository.GetPodcast(podcastId.guid);
            var selectedEpisode = selectedPodcast.Episodes.Single(x => x.Id == request.EpisodeId);
            var podcastEpisode = new PodcastEpisode(selectedPodcast, selectedEpisode);

            if (podcastEpisode == null)
            {
                throw new InvalidOperationException($"Episode with id '{request.EpisodeId}' not found");
            }

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

    private async Task PostBluesky(PodcastEpisode podcastEpisode, Uri? shortUrl)
    {
        var result = await blueSkyPoster.Post(podcastEpisode, shortUrl);
        if (result != BlueskySendStatus.Success)
        {
            logger.LogError("Forbidden to send duplicate-tweet");
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
                    logger.LogError($"Unknown tweet-send response '{result.ToString()}'.");
                    break;
            }
        }
    }

    private async Task PostNewEpisodes(PostRequest request, IList<Guid> podcastIds)
    {
        if (request.EpisodeId.HasValue)
        {
            var selectedPodcast = await repository.GetPodcast(podcastIds.Single());
            var selectedEpisode = selectedPodcast!.Episodes.Single(x => x.Id == request.EpisodeId);

            if (selectedEpisode.Ignored && request.FlipIgnored)
            {
                selectedEpisode.Ignored = false;
            }

            if (selectedEpisode.Posted || selectedEpisode.Ignored || selectedEpisode.Removed)
            {
                logger.LogWarning(
                    $"Not posting episode with id '{request.EpisodeId}'. Posted: '{selectedEpisode.Posted}', Ignored: '{selectedEpisode.Ignored}', Removed: '{selectedEpisode.Removed}'.");
            }
            else
            {
                var podcastEpisode = new PodcastEpisode(selectedPodcast, selectedEpisode);
                var result = await podcastEpisodePoster.PostPodcastEpisode(
                    podcastEpisode, request.YouTubePrimaryPostService);
                if (!result.Success)
                {
                    logger.LogError(result.ToString());
                }

                await repository.Save(selectedPodcast);
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
}