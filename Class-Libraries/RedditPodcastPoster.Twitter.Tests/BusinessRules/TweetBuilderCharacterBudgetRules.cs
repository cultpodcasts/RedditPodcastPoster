using FluentAssertions;
using RedditPodcastPoster.Twitter.Builders;

namespace RedditPodcastPoster.Twitter.Tests.BusinessRules;

public class TweetBuilderCharacterBudgetRules
{
    [Fact(DisplayName =
        "The tweet body budget before the primary URL is 257 characters, because X counts each link as a 23-character t.co URL (280 − 23).")]
    public void character_budget_before_primary_url_is_280_minus_one_tco()
    {
        // Arrange
        // Act
        var budget = TweetBuilder.CharacterBudgetBeforePrimaryUrl;
        var tco = TweetBuilder.TcoUrlCharacterWeight;

        // Assert
        tco.Should().Be(23);
        budget.Should().Be(280 - tco);
        budget.Should().Be(257);
    }

    [Fact(DisplayName =
        "When a second short URL will be appended, the title reserve is 26 (one t.co plus separator allowance), so the composed tweet stays within 280.")]
    public void second_url_reserve_is_tco_plus_separator_allowance()
    {
        // Arrange
        // Act
        var reserve = TweetBuilder.SecondUrlReserve;

        // Assert
        reserve.Should().Be(TweetBuilder.TcoUrlCharacterWeight + 3);
        reserve.Should().Be(26);
    }
}
