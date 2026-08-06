using RedditPodcastPoster.SocialPosting.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.SocialPosting.Episodes;

public record PostingResult(
    IList<ProcessResponse> Responses,
    IEnumerable<PodcastEpisode> ModifiedPodcastEpisodes);
