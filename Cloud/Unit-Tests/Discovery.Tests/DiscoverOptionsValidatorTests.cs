using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Discovery.Tests;

public class DiscoverOptionsValidatorTests
{
    private readonly DiscoverOptionsValidator _validator = new();

    [Fact]
    public void Succeeds_when_SearchSince_and_LookbackMode_are_set()
    {
        var options = new DiscoverOptions
        {
            SearchSince = "6:10:00",
            LookbackMode = DiscoveryLookbackMode.Dynamic
        };

        var result = _validator.Validate(null, options);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_LookbackMode_is_missing()
    {
        var options = new DiscoverOptions
        {
            SearchSince = "6:10:00",
            LookbackMode = null
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("LookbackMode");
        result.FailureMessage.Should().Contain("discover__LookbackMode");
    }

    [Fact]
    public void Fails_when_SearchSince_is_missing()
    {
        var options = new DiscoverOptions
        {
            SearchSince = " ",
            LookbackMode = DiscoveryLookbackMode.Static
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SearchSince");
    }

    [Theory]
    [InlineData(DiscoveryLookbackMode.Static)]
    [InlineData(DiscoveryLookbackMode.Dynamic)]
    public void Accepts_explicit_Static_or_Dynamic(DiscoveryLookbackMode mode)
    {
        var options = new DiscoverOptions
        {
            SearchSince = "6:10:00",
            LookbackMode = mode
        };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }
}
