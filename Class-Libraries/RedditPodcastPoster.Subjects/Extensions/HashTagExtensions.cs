using RedditPodcastPoster.Subjects.HashTags;

namespace RedditPodcastPoster.Subjects.Extensions;

public static class HashTagExtensions
{
    public static IEnumerable<HashTag> ToHashTags(this IEnumerable<string> hashTags)
    {
        return hashTags
            .Select(x => x.Split(" "))
            .SelectMany(x => x)
            .Distinct()
            .Select(x => new HashTag(x!, null));
    }

    public static IEnumerable<HashTag> ToHashTags(this string hashTags)
    {
        return hashTags.Split(" ")
            .Distinct()
            .Select(x => new HashTag(x!, null));
    }

    public static IEnumerable<HashTag> FromEnrichmentHashTagsToHashTags(this IEnumerable<string> enrichmentHashTags)
    {
        return enrichmentHashTags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct()
            .Select(x => new HashTag(x, (string?)
                $"#{x
                    .Replace(" ", string.Empty)
                    .Replace(".", string.Empty)
                    .Replace("'", string.Empty)
                    .Replace("-", string.Empty)}"));
    }
}