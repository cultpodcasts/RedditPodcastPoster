using Api.Resolvers;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace Api.Models;

public record PodcastEpisodeResolverResponse(Episode? Episode, Podcast? Podcast, PodcastEpisodeResolveState State);
