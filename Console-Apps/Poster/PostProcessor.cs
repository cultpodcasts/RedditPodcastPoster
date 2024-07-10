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
        IList<Podcast> podcasts;

        if (request.EpisodeId.HasValue)
        {
            var podcast = await repository.GetBy(x => x.Episodes.Any(ep => ep.Id == request.EpisodeId));
            if (podcast == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            podcasts = new[] {podcast};
        }
        else if (request.PodcastId.HasValue)
        {
            var podcast = await repository.GetPodcast(request.PodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId.Value}' not found.");
            }

            podcasts = new[] {podcast};
        }
        else if (request.PodcastName != null)
        {
            podcasts = await repository.GetAllBy(x =>
                x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase)).ToListAsync();
            logger.LogInformation($"Found {podcasts.Count()} podcasts.");
        }
        else
        {
            podcasts = await repository.GetAll().ToListAsync();
        }

        podcasts = podcasts.Where(x => !x.IsRemoved()).ToList();

        if (!request.SkipReddit)
        {
            await PostNewEpisodes(request, podcasts);
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
                var selectedPodcast = podcasts.Single(x => x.Episodes.Any(e => e.Id == request.EpisodeId));
                var selectedEpisode = selectedPodcast.Episodes.Single(x => x.Id == request.EpisodeId);
                var podcastEpisode = new PodcastEpisode(selectedPodcast, selectedEpisode);
                await tweetPoster.PostTweet(podcastEpisode);
            }
            else
            {
                var untweeted =
                    podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(podcasts, numberOfDays: request.ReleasedWithin);

                var tweeted = false;
                foreach (var podcastEpisode in untweeted)
                {
                    if (tweeted)
                    {
                        break;
                    }

                    try
                    {
                        await tweetPoster.PostTweet(podcastEpisode);
                        tweeted = true;
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

    private async Task PostNewEpisodes(PostRequest request, IList<Podcast> podcasts)
    {
        if (request.EpisodeId.HasValue)
        {
            var selectedPodcast = podcasts.Single(x => x.Episodes.Any(e => e.Id == request.EpisodeId));
            var selectedEpisode = selectedPodcast.Episodes.Single(x => x.Id == request.EpisodeId);

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
                    podcasts,
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