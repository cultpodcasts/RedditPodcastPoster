using Indexer.Activities;

namespace Indexer.Models;

/// <summary>
/// Combines parallel Indexer activity outputs that each started from the same seed context.
/// Covers every <see cref="IndexerContext"/> member <c>Indexer.Run</c> can change.
/// </summary>
public static class IndexerContextMerge
{
    public static IndexerContext Merge(IndexerContext seed, IReadOnlyList<IndexerContext> results)
    {
        if (results.Count == 0)
        {
            return seed;
        }

        return seed with
        {
            Success = results.All(r => r.Success == true),
            SkipYouTubeUrlResolving = results.Any(r => r.SkipYouTubeUrlResolving == true),
            YouTubeError = results.Any(r => r.YouTubeError == true),
            SkipSpotifyUrlResolving = results.Any(r => r.SkipSpotifyUrlResolving == true),
            SpotifyError = results.Any(r => r.SpotifyError == true),
            DuplicateIndexerPassOperations = ElementwiseOr(results.Select(r => r.DuplicateIndexerPassOperations))
        };
    }

    internal static bool[]? ElementwiseOr(IEnumerable<bool[]?> sources)
    {
        var arrays = sources.Where(a => a is not null).Cast<bool[]>().ToList();
        if (arrays.Count == 0)
        {
            return null;
        }

        var length = arrays.Max(a => a.Length);
        var merged = new bool[length];
        foreach (var array in arrays)
        {
            for (var i = 0; i < array.Length; i++)
            {
                merged[i] |= array[i];
            }
        }

        return merged;
    }
}
