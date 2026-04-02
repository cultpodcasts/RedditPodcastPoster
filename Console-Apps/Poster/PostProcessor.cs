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
    IEpisodeRepository episodeRepository,
    ITweeter tweeter,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IProcessResponsesAdaptor processResponsesAdaptor,
    IHomepagePublisher contentPublisher,
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

            var podcast = await repository.GetPodcast(episode.PodcastId);
            if (podcast == null || podcast.Removed == true)
            {
                throw new ArgumentException(
                    $"Podcast with id '{episode.PodcastId}' not found for episode-id '{request.EpisodeId.Value}' (Removed='{podcast?.Removed}').");
            }

            podcastIds = [podcast.Id];
        }
        else if (request.PodcastId.HasValue)
        {
            var podcast = await repository.GetPodcast(request.PodcastId.Value);
            if (podcast == null || podcast.Removed == true)
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
        await contentPublisher.PublishHomepage();
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

            var selectedPodcast = await repository.GetPodcast(episode.PodcastId);
            if (selectedPodcast == null || selectedPodcast.Removed == true)
            {
                throw new ArgumentException($"Podcast with id '{episode.PodcastId}' not found for episode-id '{request.EpisodeId.Value}' (Removed='{selectedPodcast?.Removed}').");
            }

            var podcastEpisode = new PodcastEpisode(selectedPodcast, episode);

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
            var selectedPodcast = await repository.GetPodcast(podcastIds.Single());
            if (selectedPodcast == null)
            {
                throw new ArgumentException($"Podcast with id '{podcastIds.Single()}' not found.");
            }

            var detachedEpisode = await episodeRepository.GetEpisode(selectedPodcast.Id, request.EpisodeId.Value);
            if (detachedEpisode == null)
            {
                throw new ArgumentException($"Episode with id '{request.EpisodeId.Value}' not found.");
            }

            if (detachedEpisode.Ignored && request.FlipIgnored)
            {
                detachedEpisode.Ignored = false;
            }

            if (detachedEpisode.Posted || detachedEpisode.Ignored || detachedEpisode.Removed)
            {
                logger.LogWarning(
                    "Not posting episode with id '{episodeId}'. Posted: '{posted}', Ignored: '{ignored}', Removed: '{removed}'.",
                    request.EpisodeId, detachedEpisode.Posted, detachedEpisode.Ignored, detachedEpisode.Removed);
            }
            else
            {
                var podcastEpisode = new PodcastEpisode(selectedPodcast, detachedEpisode);
                var result = await podcastEpisodePoster.PostPodcastEpisode(
                    podcastEpisode, request.YouTubePrimaryPostService);
                if (!result.Success)
                {
                    logger.LogError(result.ToString());
                }

                await episodeRepository.Save(detachedEpisode);
            }
        }
        else
        {
            var since = DateTimeExtensions.DaysAgo(request.ReleasedWithin);
            var allCandidateEpisodes = await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(since);
            var podcastEpisodesToPost = allCandidateEpisodes
                .Where(pe => podcastIds.Contains(pe.Episode.PodcastId))
                .ToArray();

            var postingResult = await podcastEpisodesPoster.PostNewEpisodes(
                since,
                podcastEpisodesToPost,
                preferYouTube: request.YouTubePrimaryPostService,
                ignoreAppleGracePeriod: request.IgnoreAppleGracePeriod);

            // Persist modified episodes and podcasts (save each podcast only once)
            var savedPodcasts = new HashSet<Guid>();
            foreach (var podcastEpisode in postingResult.ModifiedPodcastEpisodes)
            {
                await episodeRepository.Save(podcastEpisode.Episode);
                if (savedPodcasts.Add(podcastEpisode.Podcast.Id))
                {
                    await repository.Save(podcastEpisode.Podcast);
                }
            }

            var result = processResponsesAdaptor.CreateResponse(postingResult.Responses);
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
