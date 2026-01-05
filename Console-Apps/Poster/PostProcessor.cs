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
            var podcastId = await repository.GetBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Episodes.Any(ep => ep.Id == request.EpisodeId), x => new { guid = x.Id });
            if (podcastId == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            podcastIds = [podcastId.guid];
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

            podcastIds = [request.PodcastId.Value];
        }
        else if (request.PodcastName != null)
        {
            var ids = await repository.GetAllBy(x =>
                    (!x.Removed.IsDefined() || x.Removed == false) &&
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                x => new { guid = x.Id }).ToListAsync();
            logger.LogInformation("Found {idsCount} podcasts.", ids.Count);
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
        Task[] publishingTasks = [contentPublisher.PublishHomepage()];
        await Task.WhenAll(publishingTasks);
    }

    private async Task PostToSocial(PostRequest request)
    {
        if (request.EpisodeId.HasValue)
        {
            var podcastId = await repository.GetBy(x =>
                (!x.Removed.IsDefined() || x.Removed == false) &&
                x.Episodes.Any(ep => ep.Id == request.EpisodeId), x => new { guid = x.Id });
            if (podcastId == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            var selectedPodcast = await repository.GetPodcast(podcastId.guid);
            if (selectedPodcast != null)
            {
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
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.PodcastName) || request.PodcastId.HasValue)
            {
                Guid podcastId;
                if (!string.IsNullOrWhiteSpace(request.PodcastName))
                {
                    var podcasts = await repository.GetAllBy(x =>
                        (!x.Removed.IsDefined() || x.Removed == false) &&
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
            var selectedPodcast = await repository.GetPodcast(podcastIds.Single());
            var selectedEpisode = selectedPodcast!.Episodes.Single(x => x.Id == request.EpisodeId);

            if (selectedEpisode.Ignored && request.FlipIgnored)
            {
                selectedEpisode.Ignored = false;
            }

            if (selectedEpisode.Posted || selectedEpisode.Ignored || selectedEpisode.Removed)
            {
                logger.LogWarning(
                    "Not posting episode with id '{episodeId}'. Posted: '{posted}', Ignored: '{ignored}', Removed: '{removed}'.",
                    request.EpisodeId, selectedEpisode.Posted, selectedEpisode.Ignored, selectedEpisode.Removed);
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