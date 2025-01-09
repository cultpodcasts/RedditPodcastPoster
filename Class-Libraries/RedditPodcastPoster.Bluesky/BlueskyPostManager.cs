using idunno.AtProto.Repo;
using idunno.AtProto;
using idunno.Bluesky;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlShortening;
using RedditPodcastPoster.Bluesky.Factories;
using System.Globalization;

namespace RedditPodcastPoster.Bluesky;

public class BlueskyPostManager(
    IBlueskyPoster poster,
    IPodcastEpisodeProvider podcastEpisodeProvider,
    IShortnerService shortnerService,
    BlueskyAgent blueskyAgent,
    ILogger<BlueskyPostManager> logger)
    : IBlueskyPostManager
{
    public async Task Post(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        IEnumerable<PodcastEpisode> unposted;
        try
        {
            unposted = await podcastEpisodeProvider.GetBlueskyReadyPodcastEpisodes(youTubeRefreshed, spotifyRefreshed);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure to find podcast-episode.");
            throw;
        }
        if (unposted.Any())
        {
            var posted = false;
            foreach (var podcastEpisode in unposted)
            {
                if (posted)
                {
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
                        posted = status == BlueskySendStatus.Success;
                    }
                    catch (EpisodeNotFoundException e)
                    {
                        logger.LogError(e, "Candidate episode to post to bluesky not found");
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
    }

    public async Task<RemovePostState> RemovePost(PodcastEpisode podcastEpisode)
    {
        Nsid collection = new Nsid("app.bsky.feed.post");
        var posts = await blueskyAgent.ListRecords<AtProtoRecord>(collection, 100);
        if (!posts.Succeeded)
        {
            logger.LogError("Bluesky list-records failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
            posts.StatusCode, posts.AtErrorDetail?.Error, posts.AtErrorDetail?.Message);
        }

        var matchingPosts = posts.Result!.Where(
                x => x.Value!.ExtensionData["text"].GetString()!.Contains(podcastEpisode.Podcast.Name) &&
                      x.Value!.ExtensionData["text"].GetString()!.Contains(podcastEpisode.Episode.Length.ToString(BlueskyEmbedCardPostFactory.LengthFormat,
                         CultureInfo.InvariantCulture)) &&
                     x.Value!.ExtensionData["text"].GetString()!.Contains(podcastEpisode.Episode.Release.ToString(BlueskyEmbedCardPostFactory.ReleaseFormat))
            );

        if (!matchingPosts.Any())
        {
            return RemovePostState.NotFound;
        }

        if (matchingPosts.Count() > 1)
        {
            logger.LogError(
                $"Multiple bluesky-posts ({matchingPosts.Count()}) found matching episode-id '{podcastEpisode.Episode.Id}'");
            return RemovePostState.Other;
        }

        var deleted = await blueskyAgent.DeleteRecord(collection, matchingPosts.Single().Uri.RecordKey!);

        if (deleted.Succeeded)
        {
            return RemovePostState.Deleted;
        }

        logger.LogError("Bluesky delete-record failed. Status-code: {statusCode}, error-detail-error: {errorDetailError}, error-detail-message: {errorDetailMessage}.",
        deleted.StatusCode, deleted.AtErrorDetail?.Error, deleted.AtErrorDetail?.Message);
        return RemovePostState.Other;
    }
}