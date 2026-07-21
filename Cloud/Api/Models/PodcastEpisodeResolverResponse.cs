using Api.Resolvers;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace Api.Models;

public record PodcastEpisodeResolverResponse(Episode? Episode, Podcast? Podcast, PodcastEpisodeResolveState State);