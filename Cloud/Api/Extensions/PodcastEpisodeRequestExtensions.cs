using Api.Dtos;
using Api.Models;

namespace Api.Extensions;

public static class PodcastEpisodeRequestExtensions
{
    extension(PodcastEpisodeRequestWrapper podcastEpisodeResolverRequest)
    {
        public PodcastEpisodeResolverRequest ToPodcastEpisodeResolverRequest()
        {
            // The one place a route podcast name is normalised before it reaches the resolver.
            var podcastName = podcastEpisodeResolverRequest.PodcastName == null
                ? null
                : PodcastRouteNameNormalizer.Normalize(podcastEpisodeResolverRequest.PodcastName);
            return new PodcastEpisodeResolverRequest(podcastEpisodeResolverRequest.EpisodeId,
                podcastEpisodeResolverRequest.PodcastId, podcastName);
        }
    }

    extension(EpisodePublishRequestWrapper episodePublishRequestWrapper)
    {
        public PodcastEpisodeResolverRequest ToPodcastEpisodeResolverRequest()
        {
            return new PodcastEpisodeResolverRequest(episodePublishRequestWrapper.EpisodeId,
                episodePublishRequestWrapper.PodcastId, null);
        }
    }

    extension(EpisodeChangeRequestWrapper episodeChangeRequestWrapper)
    {
        public PodcastEpisodeResolverRequest ToPodcastEpisodeResolverRequest()
        {
            return new PodcastEpisodeResolverRequest(episodeChangeRequestWrapper.EpisodeId,
                episodeChangeRequestWrapper.PodcastId, null);
        }
    }
}