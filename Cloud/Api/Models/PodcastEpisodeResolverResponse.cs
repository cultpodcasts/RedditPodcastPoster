using Api.Resolvers;
using RedditPodcastPoster.Models;

namespace Api.Models;

public record PodcastEpisodeResolverResponse(Episode? Episode, Podcast? Podcast, PodcastEpisodeResolveState State);