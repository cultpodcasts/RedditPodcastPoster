using FluentAssertions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Configuration.Validators;
using Xunit;

namespace Indexer.Tests;

public class PostingCriteriaValidatorTests
{
    private readonly PostingCriteriaValidator _validator = new();

    [Fact]
    public void Succeeds_when_all_values_are_valid()
    {
        _validator.Validate(null, ValidOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Succeeds_when_day_counts_are_zero()
    {
        var options = ValidOptions();
        options.TweetDays = 0;
        options.RedditDays = 0;
        options.BlueSkyDays = 0;
        options.CategoriserDays = 0;

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_MinimumDuration_is_zero()
    {
        var options = ValidOptions();
        options.MinimumDuration = TimeSpan.Zero;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MinimumDuration");
    }

    [Fact]
    public void Fails_when_TweetDays_is_negative()
    {
        var options = ValidOptions();
        options.TweetDays = -1;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TweetDays");
    }

    [Fact]
    public void Fails_when_RedditDays_is_negative()
    {
        var options = ValidOptions();
        options.RedditDays = -1;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("RedditDays");
    }

    [Fact]
    public void Fails_when_BlueSkyDays_is_negative()
    {
        var options = ValidOptions();
        options.BlueSkyDays = -1;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("BlueSkyDays");
    }

    [Fact]
    public void Fails_when_CategoriserDays_is_negative()
    {
        var options = ValidOptions();
        options.CategoriserDays = -1;

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("CategoriserDays");
    }

    private static PostingCriteria ValidOptions() => new()
    {
        MinimumDuration = TimeSpan.FromMinutes(10),
        TweetDays = 2,
        RedditDays = 2,
        BlueSkyDays = 2,
        CategoriserDays = 2
    };
}
