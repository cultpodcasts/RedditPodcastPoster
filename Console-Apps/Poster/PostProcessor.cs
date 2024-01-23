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
    ITweetPoster tweetPoster,
    ILogger<PostProcessor> logger)
{
    public async Task Process(PostRequest request)
    {
        IList<Podcast> podcasts;
        if (request.PodcastId.HasValue)
        {
            var podcast = await repository.GetPodcast(request.PodcastId.Value);
            if (podcast == null)
            {
                throw new ArgumentException($"Podcast with id '{request.PodcastId.Value}' not found.");
            }

            podcasts = new[] {podcast};
        }
        else
        {
            podcasts = await repository.GetAll().ToListAsync();
        }

        await PostNewEpisodes(request, podcasts);
        Task[] publishingTasks;
        if (request.PublishSubjects)
        {
            publishingTasks = new[]
            {
                contentPublisher.PublishHomepage()
            };
        }
        else
        {
            publishingTasks = new[]
            {
                contentPublisher.PublishHomepage()
            };
        }

        await Task.WhenAll(publishingTasks);
        if (!request.SkipTweet)
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

    private async Task PostNewEpisodes(PostRequest request, IList<Podcast> podcasts)
    {
        var results =
            await podcastEpisodesPoster.PostNewEpisodes(
                DateTime.UtcNow.AddDays(-1 * request.ReleasedWithin),
                podcasts,
                preferYouTube: request.YouTubePrimaryPostService);
        var result = processResponsesAdaptor.CreateResponse(results);
        if (!result.Success)
        {
            logger.LogError(result.ToString());
        }
        else
        {
            logger.LogInformation(result.ToString());
        }
    }
}