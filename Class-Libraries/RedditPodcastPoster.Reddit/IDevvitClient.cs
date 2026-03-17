namespace RedditPodcastPoster.Reddit;

public interface IDevvitClient
{
    Task<DevvitEpisodeCreateResponse> CreateEpisodePost(
        DevvitEpisodeCreateRequest request,
        CancellationToken cancellationToken = default);
}
