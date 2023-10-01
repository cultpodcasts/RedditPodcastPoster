using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public record FilterResult(IList<(Episode, string[])> FilteredEpisodes);