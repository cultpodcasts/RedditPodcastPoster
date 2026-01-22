using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects.Extensions;

namespace RedditPodcastPoster.Subjects.HashTags;

public static class PodcastExtensions {
    public static IEnumerable<HashTag> GetHashTags(this Podcast podcast) {
        var hashTags = new List<HashTag>();
        if (!string.IsNullOrWhiteSpace(podcast.HashTag)) {
            var x = podcast.HashTag.ToHashTags();
            hashTags.AddRange(x);
        }
        if (podcast.EnrichmentHashTags != null && podcast.EnrichmentHashTags.Any()) {
            var y = podcast.EnrichmentHashTags.FromEnrichmentHashTagsToHashTags();
            hashTags.AddRange(y);
        }
        hashTags = hashTags.DistinctBy(ht => ht.Tag).ToList();
        return hashTags;
    }
}