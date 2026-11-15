using Azure.Search.Documents.Indexes.Models;

namespace RedditPodcastPoster.Search.Indexing;

/// <summary>
/// Search ranking for playable titles (RPP #1434). A film found by an alternative title, such as
/// Faderhuset by "Father's House", should rank with a film found by its title. The website and Api
/// send <c>queryType=simple</c> and <c>searchMode=any</c> with no <c>searchFields</c> and no
/// <c>scoringProfile</c>, so this profile is the index default and applies at query time. It does
/// not change field definitions or documents, and needs no indexer reset.
/// </summary>
public static class TitleMatchScoringProfile
{
    public const string Name = "titleAndAlternativeTitles";

    /// <summary>
    /// Shared weight for the title field and <c>alternativeTitles</c>. Other searchable fields
    /// (description, hidden search terms, subjects) keep Azure Search's default weight of 1, so a
    /// title or alternative-title match outranks the same terms in those fields.
    /// </summary>
    public const double Weight = 10d;

    public const string TitleField = "title";
    public const string EpisodeTitleField = "episodeTitle";
    public const string AlternativeTitlesField = "alternativeTitles";

    /// <summary>
    /// Text weights for the searchable title fields that exist on <paramref name="fields"/>.
    /// When both <c>title</c> and the legacy <c>episodeTitle</c> are present, only <c>title</c>
    /// is boosted, so a document that still carries both is not scored twice. <c>episodeTitle</c>
    /// is boosted only on an index that has no <c>title</c> field.
    /// </summary>
    public static IReadOnlyDictionary<string, double> WeightsFor(IEnumerable<SearchField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var searchable = new HashSet<string>(
            fields.Where(field => field.IsSearchable == true).Select(field => field.Name),
            StringComparer.OrdinalIgnoreCase);

        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (searchable.Contains(TitleField))
        {
            weights[TitleField] = Weight;
        }
        else if (searchable.Contains(EpisodeTitleField))
        {
            weights[EpisodeTitleField] = Weight;
        }

        if (searchable.Contains(AlternativeTitlesField))
        {
            weights[AlternativeTitlesField] = Weight;
        }

        return weights;
    }

    /// <summary>
    /// Adds or replaces this scoring profile and sets it as the index default. Existing field
    /// definitions are left unchanged. Returns <see langword="true"/> when the index object changed.
    /// </summary>
    public static bool Apply(SearchIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var weights = WeightsFor(index.Fields);
        if (weights.Count == 0)
        {
            return false;
        }

        if (ProfileAlreadyApplied(index, weights))
        {
            return false;
        }

        var profiles = index.ScoringProfiles ??
            throw new InvalidOperationException("SearchIndex.ScoringProfiles was null.");
        for (var i = profiles.Count - 1; i >= 0; i--)
        {
            if (string.Equals(profiles[i].Name, Name, StringComparison.Ordinal))
            {
                profiles.RemoveAt(i);
            }
        }

        profiles.Add(new ScoringProfile(Name)
        {
            TextWeights = new TextWeights(new Dictionary<string, double>(weights, StringComparer.OrdinalIgnoreCase))
        });
        index.DefaultScoringProfile = Name;
        return true;
    }

    private static bool ProfileAlreadyApplied(SearchIndex index, IReadOnlyDictionary<string, double> weights)
    {
        if (!string.Equals(index.DefaultScoringProfile, Name, StringComparison.Ordinal))
        {
            return false;
        }

        var existing = index.ScoringProfiles?.FirstOrDefault(profile =>
            string.Equals(profile.Name, Name, StringComparison.Ordinal));
        if (existing?.TextWeights?.Weights is not { } existingWeights)
        {
            return false;
        }

        if (existingWeights.Count != weights.Count)
        {
            return false;
        }

        foreach (var (name, weight) in weights)
        {
            if (!existingWeights.TryGetValue(name, out var existingWeight) || existingWeight != weight)
            {
                return false;
            }
        }

        return true;
    }
}
