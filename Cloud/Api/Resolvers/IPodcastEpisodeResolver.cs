using Api.Dtos;
using Api.Models;

namespace Api.Resolvers;

public interface IPodcastEpisodeResolver
{
    Task<PodcastEpisodeResolverResponse> ResolvePodcast(PodcastEpisodeResolverRequest podcastEpisodeResolverRequest, string caller);
}