using Azure.Search.Documents.Indexes.Models;

namespace RedditPodcastPoster.Search.Indexing;

/// <summary>
/// Optional named search scoring profile (RPP #1434). A film found by an alternative title,
/// such as Faderhuset by "Father's House", can rank with a film found by its title, without
/// weakening show-name matches: <c>title</c>, <c>alternativeTitles</c> and <c>seriesName</c>
/// share the same weight. It is not the index default. The Api unquoted-phrase rewrite already
/// meets the #1434 goal; a client may opt in by sending <c>scoringProfile=titleAndAlternativeTitles</c>.
/// Applying the profile does not change field definitions or documents, and needs no indexer reset.
/// </summary>
public static class TitleMatchScoringProfile
{
    public const string Name = "titleAndAlternativeTitles";

    /// <summary>
    /// Shared weight for <c>title</c>, <c>alternativeTitles</c> and <c>seriesName</c>. Other
    /// searchable fields (description, hidden search terms, subjects) keep Azure Search's default
    /// weight of 1.
    /// </summary>
    public const double Weight = 10d;

    public const string TitleField = "title";
    public const string EpisodeTitleField = "episodeTitle";
    public const string AlternativeTitlesField = "alternativeTitles";
    public const string SeriesNameField = "seriesName";
    public const string PodcastNameField = "podcastName";

    /// <summary>
    /// Text weights for the searchable title, alternative-title and series-name fields that exist
    /// on <paramref name="fields"/>. When both <c>title</c> and the legacy <c>episodeTitle</c> are
    /// present, only <c>title</c> is boosted, so a document that still carries both is not scored
    /// twice. The same applies to <c>seriesName</c> versus <c>podcastName</c>.
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

        if (searchable.Contains(SeriesNameField))
        {
            weights[SeriesNameField] = Weight;
        }
        else if (searchable.Contains(PodcastNameField))
        {
            weights[PodcastNameField] = Weight;
        }

        return weights;
    }

    /// <summary>
    /// Adds or replaces this named scoring profile. It is not set as the index default. Existing
    /// field definitions and <see cref="SearchIndex.DefaultScoringProfile"/> are left unchanged.
    /// Returns <see langword="true"/> when the index object changed.
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
        return true;
    }

    private static bool ProfileAlreadyApplied(SearchIndex index, IReadOnlyDictionary<string, double> weights)
    {
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
