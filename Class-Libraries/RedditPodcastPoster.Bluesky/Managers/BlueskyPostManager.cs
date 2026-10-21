using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Exceptions;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Bluesky.Managers;

public class BlueskyPostManager(
    IBlueskyPoster poster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
    ICataloguePlayableSocialPosts cataloguePlayableSocialPosts,
    IAsyncInstance<BlueskyAgent> blueskyAgent,
    IOptions<BlueskyOptions> options,
    ILogger<BlueskyPostManager> logger)
    : IBlueskyPostManager
{
    private readonly BlueskyOptions _options = options.Value;

    public async Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed,
        IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null)
    {
        IEnumerable<PodcastEpisode> unposted;
        try
        {
            unposted = (await podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes(
                    youTubeRefreshed,
                    spotifyRefreshed,
                    preloadedRecentCandidates))
                .ToArray();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }

        var run = new PostRun();
        if (unposted.Any())
        {
            logger.LogWarning(
                "Bluesky posting run started with {candidateCount} candidate episodes. Max-posts: {maxPosts}, Max-failures: {maxFailures}.",
                unposted.Count(),
                _options.MaxPosts,
                _options.MaxFailures);

            foreach (var podcastEpisode in unposted)
            {
                if (!await PostOne(podcastEpisode, run))
                {
                    break;
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

        // TV episodes, news reports and films follow the podcast episodes within the same max-posts and
        // failure budget. Their candidates come from their own containers, not the shared podcast cache.
        var catalogueCount = 0;
        if (!run.Stopped && run.Posts < _options.MaxPosts)
        {
            IReadOnlyList<PodcastEpisode> catalogueUnposted = [];
            try
            {
                catalogueUnposted = await cataloguePlayableSocialPosts.GetBlueskyReady();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure to find Bluesky-ready TV episodes, news reports or films.");
            }

            catalogueCount = catalogueUnposted.Count;
            foreach (var item in catalogueUnposted)
            {
                if (!await PostOne(item, run))
                {
                    break;
                }
            }
        }

        var candidateCount = unposted.Count() + catalogueCount;
        if (run.Posts == 0)
        {
            logger.LogWarning(
                "Bluesky posting run completed with zero posts. Candidate-count: {candidateCount}, failures: {failures}.",
                candidateCount,
                run.Failures);
        }
        else
        {
            logger.LogWarning(
                "Bluesky posting run completed. Posts: {posts}, candidate-count: {candidateCount}, failures: {failures}.",
                run.Posts,
                candidateCount,
                run.Failures);
        }
    }

    private sealed class PostRun
    {
        public int Posts { get; set; }
        public int Failures { get; set; }
        public bool Stopped { get; set; }
    }

    /// <summary>Posts one candidate. Returns false when the run must stop.</summary>
    private async Task<bool> PostOne(PodcastEpisode podcastEpisode, PostRun run)
    {
        if (run.Posts >= _options.MaxPosts)
        {
            logger.LogWarning(
                "Stopping Bluesky posting because max-posts limit was reached. Posts: {posts}, Max-posts: {maxPosts}.",
                run.Posts,
                _options.MaxPosts);
            run.Stopped = true;
            return false;
        }

        try
        {
            Uri? shortUrl;
            var hasShareImage = false;
            if (podcastEpisode.IsCataloguePlayable)
            {
                // The KV short link is podcast-shaped ({podcastSafeName}/{episodeId}); link to the kind route instead.
                shortUrl = CataloguePlayableSiteLink.For(podcastEpisode);
            }
            else
            {
                var shortnerResult = await shortnerService.Write(podcastEpisode);
                if (!shortnerResult.Success)
                {
                    logger.LogError("Unsuccessful shortening-url.");
                }

                shortUrl = shortnerResult.Url;
                hasShareImage = shortnerResult.HasShareImage;
            }

            logger.LogInformation("Bluesky Post init.");
            try
            {
                var status = await poster.Post(podcastEpisode, shortUrl, hasShareImage);
                logger.LogInformation("Bluesky Post complete. Bluesky-post-status: '{status}'.", status);
                if (status == BlueskySendStatus.Success)
                {
                    run.Posts++;
                    return true;
                }

                logger.LogWarning(
                    "Bluesky post not sent for podcast '{podcastName}' (podcast-id '{podcastId}') episode-id '{episodeId}'. Status: {status}.",
                    podcastEpisode.Podcast.Name,
                    podcastEpisode.Podcast.Id,
                    podcastEpisode.Episode.Id,
                    status);

                if (status is BlueskySendStatus.Failure or BlueskySendStatus.FailureAuth
                    or BlueskySendStatus.Unknown)
                {
                    run.Failures++;
                    if (run.Failures >= _options.MaxFailures)
                    {
                        logger.LogWarning(
                            "Stopping Bluesky posting because failure threshold was reached. Failures: {failures}, Max-failures: {maxFailures}.",
                            run.Failures,
                            _options.MaxFailures);
                        run.Stopped = true;
                        return false;
                    }

                    return true;
                }

                logger.LogWarning(
                    "Stopping Bluesky posting due to non-retriable status '{status}' for episode-id '{episodeId}'.",
                    status,
                    podcastEpisode.Episode.Id);
                run.Stopped = true;
                return false;
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
                "Unable to bluesky-post {contentKind} with id '{episodeId}' with title '{episodeTitle}' from parent with id '{podcastId}' and name '{podcastName}'.",
                podcastEpisode.ContentKind ?? "episode", podcastEpisode.Episode.Id, podcastEpisode.Episode.Title,
                podcastEpisode.Podcast.Id, podcastEpisode.Podcast.Name);
        }

        return true;
    }

    public async Task<RemovePostState> RemovePost(PodcastEpisode podcastEpisode)
    {
        var agent = await blueskyAgent.GetAsync();
        var collection = new Nsid("app.bsky.feed.post");

        RecordKey? recordKey;
        if (!string.IsNullOrWhiteSpace(podcastEpisode.Episode.BlueskyPost))
        {
            if (!AtUri.TryParse(podcastEpisode.Episode.BlueskyPost, out var atUri) ||
                atUri.RecordKey is null)
            {
                logger.LogError(
                    "Episode '{EpisodeId}' has BlueskyPost '{BlueskyPost}' that could not be parsed as an AT URI.",
                    podcastEpisode.Episode.Id,
                    podcastEpisode.Episode.BlueskyPost);
                return RemovePostState.Other;
            }

            recordKey = atUri.RecordKey;
        }
        else
        {
            var legacy = await ResolveRecordKeyByLegacySearch(agent, collection, podcastEpisode);
            if (legacy.Status == LegacyRecordKeyStatus.NotFound)
            {
                return RemovePostState.NotFound;
            }

            if (legacy.Status == LegacyRecordKeyStatus.Ambiguous)
            {
                return RemovePostState.Other;
            }

            recordKey = legacy.RecordKey;
        }

        var deleted = await agent.DeleteRecord(collection, recordKey!);

        if (deleted.Succeeded)
        {
            return RemovePostState.Deleted;
        }

        logger.LogError(
            "Bluesky delete-record failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
            deleted.StatusCode, deleted.AtErrorDetail?.Error, deleted.AtErrorDetail?.Message);
        return RemovePostState.Other;
    }

    private async Task<(LegacyRecordKeyStatus Status, RecordKey? RecordKey)> ResolveRecordKeyByLegacySearch(
        BlueskyAgent agent,
        Nsid collection,
        PodcastEpisode podcastEpisode)
    {
        var posts = await agent.ListRecords<AtProtoRecord>(collection, 100);
        if (!posts.Succeeded)
        {
            logger.LogError(
                "Bluesky list-records failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
                posts.StatusCode, posts.AtErrorDetail?.Error, posts.AtErrorDetail?.Message);
        }

        if (posts.Result == null)
        {
            return (LegacyRecordKeyStatus.NotFound, null);
        }

        // Same pieces as the post template, so TV, news and film posts are found too.
        var fingerprint = SocialPostRemovalFingerprint.For(podcastEpisode);
        var matchingPosts = posts.Result.Where(x =>
            x.Value.ExtensionData.TryGetValue("text", out var text) &&
            fingerprint.Matches(text.GetString())).ToArray();

        if (!matchingPosts.Any())
        {
            return (LegacyRecordKeyStatus.NotFound, null);
        }

        if (matchingPosts.Length > 1)
        {
            logger.LogError(
                "Multiple bluesky-posts ({Count}) found matching episode-id '{EpisodeId}'", matchingPosts.Length,
                podcastEpisode.Episode.Id);
            return (LegacyRecordKeyStatus.Ambiguous, null);
        }

        return (LegacyRecordKeyStatus.Found, matchingPosts.Single().Uri.RecordKey);
    }

    private enum LegacyRecordKeyStatus
    {
        Found,
        NotFound,
        Ambiguous
    }
}
