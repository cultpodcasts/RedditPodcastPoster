using FluentAssertions;
using RedditPodcastPoster.Search.Formatting;
using Xunit;
using Indexer.Activities;
using Indexer.Orchestrations;
using Indexer.Services;
using Indexer.Models;

namespace Indexer.Tests;

public class DescriptionTruncatorTests
{
    [Fact]
    public void Returns_empty_for_null_or_whitespace()
    {
        DescriptionTruncator.TruncateForSearch(null).Should().BeEmpty();
        DescriptionTruncator.TruncateForSearch(string.Empty).Should().BeEmpty();
        DescriptionTruncator.TruncateForSearch("   ").Should().BeEmpty();
        DescriptionTruncator.TruncateForSearch("\t\r\n").Should().BeEmpty();
    }

    [Fact]
    public void Returns_trimmed_text_when_within_limit()
    {
        DescriptionTruncator.TruncateForSearch("  Hello world.  ").Should().Be("Hello world.");
    }

    [Fact]
    public void Returns_unchanged_when_exactly_description_size()
    {
        var description = new string('a', Constants.DescriptionSize);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Should().Be(description);
        truncated.Should().NotEndWith("\u2026");
    }

    [Fact]
    public void Truncates_when_one_character_over_description_size()
    {
        var description = new string('x', Constants.DescriptionSize - 10) + " Salt XYZZZ";
        description.Length.Should().Be(Constants.DescriptionSize + 1);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + " Salt\u2026");
    }

    [Fact]
    public void Truncates_on_word_boundary_and_appends_ellipsis()
    {
        var description = new string('x', Constants.DescriptionSize - 10) + " Salt Lake City";
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + " Salt\u2026");
    }

    [Fact]
    public void Uses_tab_as_word_boundary()
    {
        var description = new string('x', Constants.DescriptionSize - 10) + "\tSaltLakeCityExtraStuff";
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + "\u2026");
    }

    [Fact]
    public void Ignores_word_boundary_in_first_half_of_budget()
    {
        // Only early whitespace; lastWhitespace <= budget/2 so hard-cut path is used.
        var description = "ab " + new string('x', Constants.DescriptionSize);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Should().HaveLength(Constants.DescriptionSize);
        truncated.Should().Be("ab " + new string('x', Constants.DescriptionSize - 4) + "\u2026");
    }

    [Fact]
    public void Falls_back_to_hard_cut_when_no_usable_word_boundary()
    {
        var description = new string('x', Constants.DescriptionSize + 20);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Should().HaveLength(Constants.DescriptionSize);
        truncated.Should().EndWith("\u2026");
        truncated.Should().StartWith(new string('x', Constants.DescriptionSize - 1));
    }

    [Fact]
    public void Trims_trailing_whitespace_before_ellipsis()
    {
        var description = new string('x', Constants.DescriptionSize - 10) + " Salt   Lake";
        description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        var truncated = DescriptionTruncator.TruncateForSearch(description);

        truncated.Should().Be(new string('x', Constants.DescriptionSize - 10) + " Salt\u2026");
        truncated.Should().NotContain(" \u2026");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Returns_ellipsis_prefix_when_max_length_cannot_fit_content(int maxLength)
    {
        var truncated = DescriptionTruncator.TruncateForSearch("anything long enough", maxLength);

        truncated.Should().Be("\u2026"[..maxLength]);
    }
}
