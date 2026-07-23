using FluentAssertions;
using Indexer.Models;
using Indexer.Services;
using Xunit;

namespace Indexer.Tests;

public class PosterOptionsValidatorTests
{
    private readonly PosterOptionsValidator _validator = new();

    [Fact]
    public void Succeeds_when_ReleasedDaysAgo_is_positive()
    {
        var options = new PosterOptions { ReleasedDaysAgo = 4 };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Succeeds_when_MaxPosts_is_positive()
    {
        var options = new PosterOptions { ReleasedDaysAgo = 1, MaxPosts = 10 };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_ReleasedDaysAgo_is_zero()
    {
        var options = new PosterOptions { ReleasedDaysAgo = 0 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ReleasedDaysAgo");
    }

    [Fact]
    public void Fails_when_MaxPosts_is_zero()
    {
        var options = new PosterOptions { ReleasedDaysAgo = 1, MaxPosts = 0 };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("MaxPosts");
    }
}
