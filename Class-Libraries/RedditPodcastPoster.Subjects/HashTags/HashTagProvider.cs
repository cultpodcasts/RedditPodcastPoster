using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects.Extensions;

namespace RedditPodcastPoster.Subjects.HashTags;

public class HashTagProvider(
    ISubjectRepository subjectRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<HashTagProvider> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IHashTagProvider
{
    public async Task<ICollection<HashTag>> GetHashTags(
        List<string> episodeSubjects)
    {
        var subjectRetrieval = episodeSubjects.Select(subjectRepository.GetByName).ToArray();
        var subjects = await Task.WhenAll(subjectRetrieval);
        var hashTags =subjects
                .Where(x => !string.IsNullOrWhiteSpace(x?.HashTag))
                .Select(x=>x.HashTag!)
                .ToHashTags();
        var enrichmentHashTags =
            subjects
                .Where(x => x?.EnrichmentHashTags != null && x.EnrichmentHashTags.Any())
                .SelectMany(x => x!.EnrichmentHashTags!)
                .FromEnrichmentHashTagsToHashTags();
        return enrichmentHashTags.Union(hashTags).ToList();
    }
}