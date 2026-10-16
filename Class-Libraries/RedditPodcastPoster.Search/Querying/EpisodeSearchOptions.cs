using Azure.Search.Documents;
using RedditPodcastPoster.Search.Models;

namespace RedditPodcastPoster.Search.Querying;

/// <summary>
/// The search a console tool makes when it needs the episodes that match a query.
/// The index holds every playable kind (episodes, TV show episodes, films, news reports), so the search
/// is limited to <see cref="SearchContentKind.Episode"/>, and it selects only the fields
/// <see cref="SearchDocument"/> reads, each of which is a retrievable field of the index.
/// </summary>
public static class EpisodeSearchOptions
{
    public static IReadOnlyList<string> SelectedFields { get; } =
        ["id", "title", "description", "subjects", "seriesName"];

    public static string Filter { get; } = $"contentKind eq '{SearchContentKind.Episode}'";

    public static SearchOptions Create()
    {
        var options = new SearchOptions { Filter = Filter };
        foreach (var field in SelectedFields)
        {
            options.Select.Add(field);
        }

        return options;
    }
}