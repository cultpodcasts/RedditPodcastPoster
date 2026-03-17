using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;

namespace DevvitClient;

public class DevvitClientProcessor(
    IDevvitClient devvitClient,
    IEpisodeRepository episodeRepository,
    IPodcastRepositoryV2 podcastRepository,
    ILogger<DevvitClientProcessor> logger)
{
    public async Task Run(DevvitClientRequest request)
    {
        var createRequest = await BuildFromEpisodeId(request.EpisodeId);

        var result = await devvitClient.CreateEpisodePost(createRequest);
        logger.LogInformation("Posted Devvit episode successfully. PostId='{PostId}' Url='{PostUrl}'.", result.PostId,
            result.PostUrl);
    }

    private async Task<DevvitEpisodeCreateRequest> BuildFromEpisodeId(Guid episodeId)
    {
        var episode = await episodeRepository.GetBy(x => x.Id == episodeId);
        if (episode == null)
        {
            throw new InvalidOperationException($"Episode '{episodeId}' not found.");
        }

        var podcast = await podcastRepository.GetPodcast(episode.PodcastId);
        if (podcast == null)
        {
            throw new InvalidOperationException(
                $"Podcast '{episode.PodcastId}' not found for episode '{episodeId}'.");
        }

        return new DevvitEpisodeCreateRequest
        {
            PodcastName = podcast.Name,
            Title = episode.Title,
            Description = episode.Description,
            ReleaseDateTime = episode.Release.ToUniversalTime(),
            Duration = episode.Length,
            ImageUrl = episode.Images?.YouTube
                ?? episode.Images?.Spotify
                ?? episode.Images?.Apple,
            ServiceLinks = new DevvitServiceLinks
            {
                YouTube = episode.Urls.YouTube,
                Spotify = episode.Urls.Spotify,
                Apple = episode.Urls.Apple
            }
        };
    }
}
