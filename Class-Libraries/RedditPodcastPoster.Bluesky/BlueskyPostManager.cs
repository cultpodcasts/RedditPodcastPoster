using System.Globalization;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlShortening;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPostManager(
    IBlueskyPoster poster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
    IAsyncInstance<BlueskyAgent> blueskyAgent,
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyPostManager> logger)
    : IBlueskyPostManager
{
    private readonly BlueskyOptions _options = options.Value;

    public async Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        IEnumerable<PodcastEpisode> unposted;
        try
        {
            unposted = (await podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes(youTubeRefreshed, spotifyRefreshed))
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        var failures = 0;
        var posts = 0;
        if (unposted.Any())
        {
            logger.LogWarning(
                "Bluesky posting run started with {candidateCount} candidate episodes. Max-posts: {maxPosts}, Max-failures: {maxFailures}.",
                unposted.Count(),
                _options.MaxPosts,
                _options.MaxFailures);

            foreach (var podcastEpisode in unposted)
            {
                if (posts >= _options.MaxPosts)
                {
                    logger.LogWarning(
                        "Stopping Bluesky posting because max-posts limit was reached. Posts: {posts}, Max-posts: {maxPosts}.",
                        posts,
                        _options.MaxPosts);
                    break;
                }

                try
                {
                    var shortnerResult = await shortnerService.Write(podcastEpisode);
                    if (!shortnerResult.Success)
                    {
                        logger.LogError("Unsuccessful shortening-url.");
                    }

                    logger.LogInformation("Bluesky Post init.");
                    try
                    {
                        var status = await poster.Post(podcastEpisode, shortnerResult.Url);
                        logger.LogInformation("Bluesky Post complete. Bluesky-post-status: '{status}'.", status);
                        var posted = status == BlueskySendStatus.Success;

                        if (!posted)
                        {
                            logger.LogWarning(
                                "Bluesky post not sent for podcast '{podcastName}' (podcast-id '{podcastId}') episode-id '{episodeId}'. Status: {status}.",
                                podcastEpisode.Podcast.Name,
                                podcastEpisode.Podcast.Id,
                                podcastEpisode.Episode.Id,
                                status);

                            if (status is BlueskySendStatus.Failure or BlueskySendStatus.FailureAuth
                                or BlueskySendStatus.Unknown)
                            {
                                failures++;
                                if (failures >= _options.MaxFailures)
                                {
                                    logger.LogWarning(
                                        "Stopping Bluesky posting because failure threshold was reached. Failures: {failures}, Max-failures: {maxFailures}.",
                                        failures,
                                        _options.MaxFailures);
                                    break;
                                }
                            }
                            else
                            {
                                logger.LogWarning(
                                    "Stopping Bluesky posting due to non-retriable status '{status}' for episode-id '{episodeId}'.",
                                    status,
                                    podcastEpisode.Episode.Id);
                                break;
                            }
                        }
                        else
                        {
                            posts++;
                        }
                    }
                    catch (EpisodeNotFoundException e)
                    {
                        logger.LogError(e,
                            "Candidate episode to post to bluesky not found. Podcast '{podcastName}' with podcast-id '{podcastId}' and episode-id '{episodeId}'.",
                            podcastEpisode.Podcast.Name, podcastEpisode.Podcast.Id, podcastEpisode.Episode.Id);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Unable to bluesky-post episode with id '{episodeId}' with title '{episodeTitle}' from podcast with id '{podcastId}' and name '{podcastName}'.",
                        podcastEpisode.Episode.Id, podcastEpisode.Episode.Title, podcastEpisode.Podcast.Id,
                        podcastEpisode.Podcast.Name);
                }
            }
        }
        else
        {
            logger.LogWarning(
                "Bluesky posting skipped because no candidate episodes were returned from the provider. youTubeRefreshed: {youTubeRefreshed}, spotifyRefreshed: {spotifyRefreshed}.",
                youTubeRefreshed,
                spotifyRefreshed);
        }

        if (posts == 0)
        {
            logger.LogWarning(
                "Bluesky posting run completed with zero posts. Candidate-count: {candidateCount}, failures: {failures}.",
                unposted.Count(),
                failures);
        }
        else
        {
            logger.LogWarning(
                "Bluesky posting run completed. Posts: {posts}, candidate-count: {candidateCount}, failures: {failures}.",
                posts,
                unposted.Count(),
                failures);
        }
    }

    public async Task<RemovePostState> RemovePost(PodcastEpisode podcastEpisode)
    {
        var agent = await blueskyAgent.GetAsync();
        var collection = new Nsid("app.bsky.feed.post");
        var posts = await agent.ListRecords<AtProtoRecord>(collection, 100);
        if (!posts.Succeeded)
        {
            logger.LogError(
                "Bluesky list-records failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
                posts.StatusCode, posts.AtErrorDetail?.Error, posts.AtErrorDetail?.Message);
        }

        var matchingPosts = posts.Result!.Where(x =>
            x.Value.ExtensionData["text"].GetString()!.Contains(podcastEpisode.Podcast.Name) &&
            x.Value.ExtensionData["text"].GetString()!.Contains(podcastEpisode.Episode.Length.ToString(
                BlueskyEmbedCardPostFactory.LengthFormat,
                CultureInfo.InvariantCulture)) &&
            x.Value.ExtensionData["text"].GetString()!.Contains(
                podcastEpisode.Episode.Release.ToString(BlueskyEmbedCardPostFactory.ReleaseFormat))
        ).ToArray();

        if (!matchingPosts.Any())
        {
            return RemovePostState.NotFound;
        }

        if (matchingPosts.Length > 1)
        {
            logger.LogError(
                "Multiple bluesky-posts ({Count}) found matching episode-id '{EpisodeId}'", matchingPosts.Length,
                podcastEpisode.Episode.Id);
            return RemovePostState.Other;
        }

        var deleted = await agent.DeleteRecord(collection, matchingPosts.Single().Uri.RecordKey!);

        if (deleted.Succeeded)
        {
            return RemovePostState.Deleted;
        }

        logger.LogError(
            "Bluesky delete-record failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
            deleted.StatusCode, deleted.AtErrorDetail?.Error, deleted.AtErrorDetail?.Message);
        return RemovePostState.Other;
    }
}

