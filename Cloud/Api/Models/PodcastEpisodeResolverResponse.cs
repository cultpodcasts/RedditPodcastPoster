using Api.Resolvers;
using RedditPodcastPoster.Models.V2;

namespace Api.Models;

public record PodcastEpisodeResolverResponse(Episode? Episode, Podcast? Podcast, PodcastEpisodeResolveState State);