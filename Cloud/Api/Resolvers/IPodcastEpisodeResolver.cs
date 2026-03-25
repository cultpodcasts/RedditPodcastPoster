using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Models.V2;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace Api.Resolvers;

public interface IPodcastEpisodeResolver
{
    Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(
        PodcastEpisodeResolverRequest podcastEpisodeResolverRequest, string caller);

    Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(PodcastEpisodeRequestWrapper podcastEpisodeResolverRequest, string caller);
    Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(EpisodePublishRequestWrapper podcastEpisodeResolverRequest, string caller);
    Task<(Episode? episode, Podcast? podcast)> ResolvePodcast(EpisodeChangeRequestWrapper podcastEpisodeResolverRequest, string caller);
}