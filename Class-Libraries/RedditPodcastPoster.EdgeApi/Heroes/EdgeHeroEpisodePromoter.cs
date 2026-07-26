using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EdgeApi.Clients;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;

namespace RedditPodcastPoster.EdgeApi.Heroes;

public sealed class EdgeHeroEpisodePromoter(
    IApiClient apiClient,
    ILogger<EdgeHeroEpisodePromoter> logger) : IHeroEpisodePromoter
{
    public async Task PromoteAsync(IReadOnlyList<Guid> episodeIds, CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return;
        }

        var episodeIdList = string.Join(',', episodeIds);
        try
        {
            await apiClient.AppendHeroEpisodes(episodeIds, cancellationToken);
            logger.LogInformation(
                "Hero auto-promote: posted {Count} episode(s) to edge hero curation. EpisodeIds: {EpisodeIds}.",
                episodeIds.Count,
                episodeIdList);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Hero auto-promote: failed posting {Count} episode(s) to edge hero curation; episode pipeline continues. EpisodeIds: {EpisodeIds}.",
                episodeIds.Count,
                episodeIdList);
        }
    }
}
