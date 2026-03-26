using System.Web;
using Api.Dtos;
using Api.Models;

namespace Api.Extensions;

public static class PodcastEpisodeRequestExtensions
{
    extension(PodcastEpisodeRequestWrapper podcastEpisodeResolverRequest)
    {
        public PodcastEpisodeResolverRequest ToPodcastEpisodeResolverRequest()
        {
            return new PodcastEpisodeResolverRequest(podcastEpisodeResolverRequest.EpisodeId,
                podcastEpisodeResolverRequest.PodcastId, HttpUtility.UrlDecode(podcastEpisodeResolverRequest.PodcastName));
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