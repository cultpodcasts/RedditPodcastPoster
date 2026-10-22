using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Orchestrations;
using Indexer.Services;
using Xunit;
using RedditPodcastPoster.Search.Formatting;

namespace Indexer.Tests;

public class DescriptionTruncatorTests
{
    [Fact(DisplayName =
        "When the description is null, empty or whitespace, then the search description is empty, because there is nothing to show on the card.")]
    public void Returns_empty_for_null_or_whitespace()
    {
        // Arrange
        string?[] inputs = [null, string.Empty, "   ", "\t\r\n"];

        // Act
        var truncated = inputs.Select(DescriptionTruncator.TruncateForSearch);

        // Assert
        truncated.Should().AllBe(string.Empty);
    }

    [Fact(DisplayName =
        "When the description fits the cap, then it is only trimmed, because no ellipsis is needed.")]
    public void Returns_trimmed_text_when_within_limit()
    {
        // Arrange
        const string description = "  Hello world.  ";

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().Be("Hello world.");
    }

    [Fact(DisplayName =
        "When the description is exactly the cap, then it is kept whole without an ellipsis, because the cap is inclusive.")]
    public void Returns_unchanged_when_exactly_description_size()
    {
        // Arrange
        var description = new string('a', Constants.DescriptionSize);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().Be(description);
        truncated.Should().NotEndWith("\u2026");
    }

    [Fact(DisplayName =
        "When the description is one character over the cap, then it is cut at the last word boundary with an ellipsis, because the result must fit the cap.")]
    public void Truncates_when_one_character_over_description_size()
    {
        // Arrange
        var description = new string('x', Constants.DescriptionSize - 10) + " Salt XYZZZ";
        description.Length.Should().Be(Constants.DescriptionSize + 1);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + " Salt\u2026");
    }

    [Fact(DisplayName =
        "When the description is over the cap, then it is cut on a word boundary and ends with an ellipsis, because cards should not show half words.")]
    public void Truncates_on_word_boundary_and_appends_ellipsis()
    {
        // Arrange
        var description = new string('x', Constants.DescriptionSize - 10) + " Salt Lake City";
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + " Salt\u2026");
    }

    [Fact(DisplayName =
        "When the only late word boundary is a tab, then the cut uses the tab, because any whitespace counts as a word boundary.")]
    public void Uses_tab_as_word_boundary()
    {
        // Arrange
        var description = new string('x', Constants.DescriptionSize - 10) + "\tSaltLakeCityExtraStuff";
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + "\u2026");
    }

    [Fact(DisplayName =
        "When the only word boundary is in the first half of the budget, then the text is hard-cut, because cutting there would lose too much text.")]
    public void Ignores_word_boundary_in_first_half_of_budget()
    {
        // Arrange
        var description = "ab " + new string('x', Constants.DescriptionSize);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().HaveLength(Constants.DescriptionSize);
        truncated.Should().Be("ab " + new string('x', Constants.DescriptionSize - 4) + "\u2026");
    }

    [Fact(DisplayName =
        "When there is no word boundary at all, then the text is hard-cut to the cap with an ellipsis, because the result must still fit.")]
    public void Falls_back_to_hard_cut_when_no_usable_word_boundary()
    {
        // Arrange
        var description = new string('x', Constants.DescriptionSize + 20);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().HaveLength(Constants.DescriptionSize);
        truncated.Should().EndWith("\u2026");
        truncated.Should().StartWith(new string('x', Constants.DescriptionSize - 1));
    }

    [Fact(DisplayName =
        "When whitespace precedes the cut, then it is trimmed before the ellipsis, because a space before the ellipsis wastes a character.")]
    public void Trims_trailing_whitespace_before_ellipsis()
    {
        // Arrange
        var description = new string('x', Constants.DescriptionSize - 10) + " Salt   Lake";
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + " Salt\u2026");
        truncated.Should().NotContain(" \u2026");
    }

    [Theory(DisplayName =
        "When the max length cannot fit any content, then only as much of the ellipsis as fits is returned, because the result must never exceed the max length.")]
    [InlineData(0)]
    [InlineData(1)]
    public void Returns_ellipsis_prefix_when_max_length_cannot_fit_content(int maxLength)
    {
        // Arrange
        const string description = "anything long enough";

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description, maxLength);

        // Assert
        truncated.Should().Be("\u2026"[..maxLength]);
    }

    [Fact(DisplayName =
        "The search description cap is 50 characters, because cap 100 filled the Free-tier 50 MB index (22 Oct 2026).")]
    public void Description_cap_is_50_for_the_free_tier_index()
    {
        // Arrange
        // Act
        var cap = Constants.DescriptionSize;

        // Assert
        cap.Should().Be(50);
    }

    [Fact(DisplayName =
        "A long description is cut to at most 50 characters at the last word boundary, with an ellipsis.")]
    public void Truncates_long_description_to_fifty_characters_at_a_word_boundary()
    {
        // Arrange
        const string description =
            "Former members describe life inside the compound and the years it took to leave.";

        // Act
        var truncated = DescriptionTruncator.TruncateForSearch(description);

        // Assert
        truncated.Should().Be("Former members describe life inside the compound\u2026");
        truncated.Length.Should().BeLessThanOrEqualTo(50);
    }

    [Fact(DisplayName =
        "The Cosmos SQL cut uses the same 50 cap as the push path: keep up to 50, else a 49-character slice plus an ellipsis, preferring a space past the halfway point.")]
    public void Cosmos_sql_uses_the_fifty_character_cap()
    {
        // Arrange
        // Act
        var sql = DescriptionTruncator.CosmosSql("e.description");

        // Assert
        sql.Should().Contain("LENGTH(LTRIM(RTRIM(e.description))) <= 50");
        sql.Should().Contain("SUBSTRING(LTRIM(RTRIM(e.description)), 0, 49)");
        sql.Should().Contain(") > 24,");
        sql.Should().NotContain("<= 100");
    }
}
