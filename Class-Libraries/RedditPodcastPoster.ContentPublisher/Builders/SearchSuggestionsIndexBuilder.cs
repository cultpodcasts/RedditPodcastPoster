using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Builders;

/// <summary>
/// Builds the flat typeahead match index from subjects (name + aliases) and
/// non-removed podcast names. AssociatedSubjects are intentionally excluded.
/// </summary>
public class SearchSuggestionsIndexBuilder(
    ISubjectRepository subjectRepository,
    IPodcastRepository podcastRepository) : ISearchSuggestionsIndexBuilder
{
    public async Task<SearchSuggestionsCorpus> BuildAsync(CancellationToken cancellationToken = default)
    {
        var subjects = new List<Subject>();
        await foreach (var subject in subjectRepository.GetAll().WithCancellation(cancellationToken))
        {
            subjects.Add(subject);
        }

        var podcasts = new List<Podcast>();
        await foreach (var podcast in podcastRepository.GetAll().WithCancellation(cancellationToken))
        {
            podcasts.Add(podcast);
        }

        return Build(subjects, podcasts);
    }

    /// <summary>
    /// Pure builder for tests and callers that already hold in-memory collections.
    /// </summary>
    public static SearchSuggestionsCorpus Build(
        IEnumerable<Subject> subjects,
        IEnumerable<Podcast> podcasts,
        DateTime? generatedAtUtc = null)
    {
        var entries = new List<SearchSuggestionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string type, string canonical, string sourceText, string? alias = null)
        {
            var trimmedCanonical = canonical.Trim();
            var searchText = sourceText.Trim().ToLowerInvariant();
            if (trimmedCanonical.Length == 0 || searchText.Length == 0)
            {
                return;
            }

            var key = $"{type}\0{trimmedCanonical}\0{searchText}";
            if (!seen.Add(key))
            {
                return;
            }

            entries.Add(new SearchSuggestionEntry(type, trimmedCanonical, searchText, alias));
        }

        foreach (var subject in subjects)
        {
            if (string.IsNullOrWhiteSpace(subject.Name))
            {
                continue;
            }

            var name = subject.Name.Trim();
            Add("subject", name, name);

            foreach (var rawAlias in subject.Aliases ?? Array.Empty<string>())
            {
                var alias = rawAlias.Trim();
                if (alias.Length == 0)
                {
                    continue;
                }

                Add("subject", name, alias, alias);
            }
        }

        foreach (var podcast in podcasts)
        {
            if (string.IsNullOrWhiteSpace(podcast.Name))
            {
                continue;
            }

            if (podcast.Removed == true)
            {
                continue;
            }

            var name = podcast.Name.Trim();
            Add("podcast", name, name);
        }

        var ordered = entries
            .OrderBy(e => e.SearchText, StringComparer.Ordinal)
            .ThenBy(e => e.Type, StringComparer.Ordinal)
            .ThenBy(e => e.Canonical, StringComparer.Ordinal)
            .ToArray();

        return new SearchSuggestionsCorpus(generatedAtUtc ?? DateTime.UtcNow, ordered);
    }
}
