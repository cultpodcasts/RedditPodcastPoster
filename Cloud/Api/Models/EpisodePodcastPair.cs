using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace Api.Models;

public record EpisodePodcastPair(Episode Episode, Podcast Podcast);
