using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Builders;

/// <summary>
/// Builds the flat typeahead match index from subjects (name + aliases), non-removed podcast,
/// TV show and news organisation names, and non-removed, non-ignored film names (with the film id).
/// AssociatedSubjects are intentionally excluded.
/// </summary>
public class SearchSuggestionsIndexBuilder(
    ISubjectRepository subjectRepository,
    IPodcastRepository podcastRepository,
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IFilmRepository filmRepository) : ISearchSuggestionsIndexBuilder
{
    public const string SubjectType = "subject";
    public const string PodcastType = "podcast";
    public const string TvShowType = "tvShow";
    public const string NewsOrganisationType = "newsOrganisation";
    public const string FilmType = "film";

    public async Task<SearchSuggestionsCorpus> BuildAsync(CancellationToken cancellationToken = default)
    {
        var subjects = await ToList(subjectRepository.GetAll(), cancellationToken);
        var podcasts = await ToList(podcastRepository.GetAll(), cancellationToken);
        var tvShows = await ToList(tvShowRepository.GetAll(), cancellationToken);
        var newsOrganisations = await ToList(newsOrganisationRepository.GetAll(), cancellationToken);
        var films = await ToList(filmRepository.GetAll(), cancellationToken);

        return Build(subjects, podcasts, tvShows, newsOrganisations, films);
    }

    private static async Task<List<T>> ToList<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken)
    {
        var items = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Pure builder for tests and callers that already hold in-memory collections.
    /// </summary>
    public static SearchSuggestionsCorpus Build(
        IEnumerable<Subject> subjects,
        IEnumerable<Podcast> podcasts,
        IEnumerable<TvShow> tvShows,
        IEnumerable<NewsOrganisation> newsOrganisations,
        IEnumerable<Film> films,
        DateTime? generatedAtUtc = null)
    {
        var entries = new List<SearchSuggestionEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string type, string canonical, string sourceText, string? alias = null, string? id = null)
        {
            var trimmedCanonical = canonical.Trim();
            var searchText = sourceText.Trim().ToLowerInvariant();
            if (trimmedCanonical.Length == 0 || searchText.Length == 0)
            {
                return;
            }

            // Rows with an id (films) key on it, so two films with the same name both stay.
            var key = $"{type}\0{id ?? trimmedCanonical}\0{searchText}";
            if (!seen.Add(key))
            {
                return;
            }

            entries.Add(new SearchSuggestionEntry(type, trimmedCanonical, searchText, alias, id));
        }

        void AddName(string type, string? name, string? id = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var trimmed = name.Trim();
            Add(type, trimmed, trimmed, id: id);
        }

        foreach (var subject in subjects)
        {
            if (string.IsNullOrWhiteSpace(subject.Name))
            {
                continue;
            }

            var name = subject.Name.Trim();
            Add(SubjectType, name, name);

            foreach (var rawAlias in subject.Aliases ?? Array.Empty<string>())
            {
                var alias = rawAlias.Trim();
                if (alias.Length == 0)
                {
                    continue;
                }

                Add(SubjectType, name, alias, alias);
            }
        }

        foreach (var podcast in podcasts.Where(p => p.Removed != true))
        {
            AddName(PodcastType, podcast.Name);
        }

        foreach (var tvShow in tvShows.Where(t => !t.IsRemoved()))
        {
            AddName(TvShowType, tvShow.Name);
        }

        foreach (var newsOrganisation in newsOrganisations.Where(n => !n.IsRemoved()))
        {
            AddName(NewsOrganisationType, newsOrganisation.Name);
        }

        foreach (var film in films.Where(f => !f.IsRemoved() && !f.Ignored))
        {
            AddName(FilmType, film.Name, film.Id.ToString());
        }

        var ordered = entries
            .OrderBy(e => e.SearchText, StringComparer.Ordinal)
            .ThenBy(e => e.Type, StringComparer.Ordinal)
            .ThenBy(e => e.Canonical, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToArray();

        return new SearchSuggestionsCorpus(generatedAtUtc ?? DateTime.UtcNow, ordered);
    }
}
