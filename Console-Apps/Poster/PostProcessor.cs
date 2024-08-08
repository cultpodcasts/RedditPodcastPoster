using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Twitter;

namespace Poster;

public class PostProcessor(
    IPodcastRepository repository,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    IContentPublisher contentPublisher,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IPodcastEpisodePoster podcastEpisodePoster,
    ITweetPoster tweetPoster,
    ILogger<PostProcessor> logger)
{
    public async Task Process(PostRequest request)
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
            var since = DateTime.UtcNow.AddDays(-1 * request.ReleasedWithin);
            var ids = await repository.GetPodcastsIdsWithUnpostedReleasedSince(since);
            podcastIds = ids.ToList();
        }


        if (!request.SkipReddit)
        {
            await PostNewEpisodes(request, podcastIds);
        }

        Task[] publishingTasks =
        {
            contentPublisher.PublishHomepage()
        };

        await Task.WhenAll(publishingTasks);
        if (!request.SkipTweet)
        {
            if (request.EpisodeId.HasValue)
            {
                var selectedPodcast = await repository.GetPodcast(podcastIds.Single());
                var selectedEpisode = selectedPodcast.Episodes.Single(x => x.Id == request.EpisodeId);
                var podcastEpisode = new PodcastEpisode(selectedPodcast, selectedEpisode);
                await tweetPoster.PostTweet(podcastEpisode);
            }
            else
            {
                var since = DateTime.UtcNow.AddDays(-1 * request.ReleasedWithin);
                var untweetedPodcastIds = await repository.GetPodcastIdsWithUntweetedReleasedSince(since);
                var untweeted = new List<PodcastEpisode>();
                foreach (var podcastId in untweetedPodcastIds)
                {
                    var podcast = await repository.GetPodcast(podcastId);
                    var filtered =
                        podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(podcast,
                            numberOfDays: request.ReleasedWithin);
                    untweeted.AddRange(filtered);
                }


                var tweeted = false;
                var tooManyRequests = false;
                foreach (var podcastEpisode in untweeted)
                {
                    if (tweeted || tooManyRequests)
                    {
                        break;
                    }

                    try
                    {
                        var result = await tweetPoster.PostTweet(podcastEpisode);
                        tweeted = result == TweetSendStatus.Sent;
                        tooManyRequests = result == TweetSendStatus.TooManyRequests;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            $"Unable to tweet episode with id '{podcastEpisode.Episode.Id}' with title '{podcastEpisode.Episode.Title}' from podcast with id '{podcastEpisode.Podcast.Id}' and name '{podcastEpisode.Podcast.Name}'.");
                    }
                }
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
                    DateTime.UtcNow.AddDays(-1 * request.ReleasedWithin),
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