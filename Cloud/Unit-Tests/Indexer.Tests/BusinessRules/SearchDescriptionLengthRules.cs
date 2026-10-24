using FluentAssertions;
using RedditPodcastPoster.Search.Formatting;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class SearchDescriptionLengthRules
{
    [Fact(DisplayName =
        "The search description cap is 80 characters: at ~41.55 MB of a 50 MB Free-tier index " +
        "(≈8.45 MB headroom), +30 chars from the Oct 2026 emergency 50-cap stays under quota, " +
        "while restoring 100/180/230 would risk the overrun that forced the cut.")]
    public void Search_description_cap_is_eighty_for_free_tier_headroom()
    {
        // Arrange
        // Act
        var cap = Constants.DescriptionSize;

        // Assert
        cap.Should().Be(80);
    }

    [Fact(DisplayName =
        "A long description is cut to at most 80 characters at the last word boundary, with an ellipsis.")]
    public void Long_description_is_cut_to_eighty_characters_at_a_word_boundary()
    {
        // Arrange
        const string description =
            "Former members describe life inside the compound and the years it took to leave safely.";

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        truncated.Should().EndWith("\u2026");
        truncated.Should().Be(
            "Former members describe life inside the compound and the years it took to\u2026");
    }
}
