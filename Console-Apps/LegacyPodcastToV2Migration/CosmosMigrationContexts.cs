using Microsoft.Azure.Cosmos;

namespace LegacyPodcastToV2Migration;

public sealed record LegacyCosmosContext(
    CosmosClient Client,
    Container LegacyContainer);

public sealed record TargetCosmosContext(
    CosmosClient Client,
    Container PodcastsContainer,
    Container EpisodesContainer);
