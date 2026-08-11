using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Factories;

public class SubjectEnrichmentOptionsFactory(
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider)
    : ISubjectEnrichmentOptionsFactory
{
    public async Task<SubjectEnrichmentOptions> CreateAsync(
        Podcast podcast,
        Episode? episode = null,
        CancellationToken cancellationToken = default)
    {
        var language = !string.IsNullOrWhiteSpace(episode?.Language)
            ? episode.Language
            : podcast.Language;

        var provider = await titleCasingRulesProvider.GetAsync(cancellationToken);
        var languageIgnored = await provider.GetIgnoredSubjectsAsync(language, cancellationToken);
        var merged = UnionIgnoreLists(podcast.IgnoredSubjects, languageIgnored);

        return new SubjectEnrichmentOptions(
            podcast.IgnoredAssociatedSubjects,
            merged,
            podcast.DefaultSubject,
            podcast.DescriptionRegex ?? string.Empty);
    }

    public static string[]? UnionIgnoreLists(
        IEnumerable<string>? podcastIgnored,
        IEnumerable<string>? languageIgnored)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (podcastIgnored is not null)
        {
            foreach (var item in podcastIgnored)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    set.Add(item.Trim());
                }
            }
        }

        if (languageIgnored is not null)
        {
            foreach (var item in languageIgnored)
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    set.Add(item.Trim());
                }
            }
        }

        return set.Count == 0 ? null : set.ToArray();
    }
}
