using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.HashTags;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class HashTagExtensions
{
    public static IEnumerable<HashTag> ToHashTags(this IEnumerable<string> hashTags)
    {
        return hashTags
            .Select(x => x.Split(" "))
            .SelectMany(x => x)
            .Distinct()
            .Where(HashtagText.IsPermittedHashTag)
            .Select(x => new HashTag(x!, null));
    }

    public static IEnumerable<HashTag> ToHashTags(this string hashTags)
    {
        return hashTags.Split(" ")
            .Distinct()
            .Where(HashtagText.IsPermittedHashTag)
            .Select(x => new HashTag(x!, null));
    }

    public static IEnumerable<HashTag> FromEnrichmentHashTagsToHashTags(this IEnumerable<string> enrichmentHashTags)
    {
        return enrichmentHashTags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct()
            .Select(tag => (Tag: tag, Body: HashtagText.ToEnrichmentHashtagBody(tag)))
            .Where(x => x.Body is not null)
            .Select(x => new HashTag(x.Tag, $"#{x.Body}"));
    }
}
