using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Common.Episodes;

public record PostingResult(
    IList<ProcessResponse> Responses,
    IEnumerable<PodcastEpisode> ModifiedPodcastEpisodes);
