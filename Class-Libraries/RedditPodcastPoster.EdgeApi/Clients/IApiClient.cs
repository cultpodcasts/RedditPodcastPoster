namespace RedditPodcastPoster.EdgeApi.Clients;

public interface IApiClient
{
    Task Test();

    Task AppendHeroEpisodes(IReadOnlyList<Guid> episodeIds, CancellationToken cancellationToken = default);
}
