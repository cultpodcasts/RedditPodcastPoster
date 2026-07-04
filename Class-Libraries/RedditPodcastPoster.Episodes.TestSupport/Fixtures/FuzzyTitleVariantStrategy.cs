namespace RedditPodcastPoster.Episodes.TestSupport.Fixtures;

/// <summary>
/// Word-level transforms applied by <see cref="DomainTestFixture.CreateFuzzyTitleVariant(string, FuzzyTitleVariantStrategy)"/>.
/// Each value corresponds to one deterministic mutation used to probe fuzzy title matching
/// (catalogue wording drift, filler insertion, adjacent-word swap).
/// </summary>
public enum FuzzyTitleVariantStrategy
{
    /// <summary>Replace one word in the title with a length-similar alternative from the fixture word list.</summary>
    ReplaceWord,

    /// <summary>Drop one non-first, non-last word from the title (drops middle when possible).</summary>
    DropWord,

    /// <summary>Prepend a short filler word (The/A/An) to the title.</summary>
    AddFillerWord,

    /// <summary>Swap two adjacent words in the title.</summary>
    SwapAdjacentWords
}
