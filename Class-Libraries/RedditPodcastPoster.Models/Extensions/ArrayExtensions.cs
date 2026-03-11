namespace RedditPodcastPoster.Models.Extensions;

public static class ArrayExtensions
{
    extension(string[]? a)
    {
        public bool AreOrderedEqual(string[]? b, StringComparer? comparer = null)
        {
            comparer ??= StringComparer.Ordinal;
            return (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>(), comparer);
        }

        public bool AreSetEquivalent(string[]? b, StringComparer? comparer = null)
        {
            comparer ??= StringComparer.OrdinalIgnoreCase;
            return new HashSet<string>(a ?? Array.Empty<string>(), comparer).SetEquals(b ?? Array.Empty<string>());
        }

        public bool AreMultisetEquivalent(string[]? b, StringComparer? comparer = null)
        {
            comparer ??= StringComparer.OrdinalIgnoreCase;
            var left = (a ?? Array.Empty<string>()).OrderBy(x => x, comparer).ToArray();
            var right = (b ?? Array.Empty<string>()).OrderBy(x => x, comparer).ToArray();
            return left.SequenceEqual(right, comparer);
        }
    }
}