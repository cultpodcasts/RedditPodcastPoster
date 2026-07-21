using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using Discovery.Activities;
using Discovery.Orchestrations;
using Discovery.Services;
using Discovery.Models;

namespace Discovery.Tests;

public class DiscoverOptionsValidatorTests
{
    private readonly DiscoverOptionsValidator _validator = new();

    [Fact]
    public void Succeeds_when_options_are_default()
    {
        var options = new DiscoverOptions();

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Succeeds_when_DynamicLookbackOverlap_is_zero()
    {
        var options = new DiscoverOptions { DynamicLookbackOverlap = TimeSpan.Zero };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_DynamicLookbackOverlap_is_negative()
    {
        var options = new DiscoverOptions { DynamicLookbackOverlap = TimeSpan.FromMinutes(-1) };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DynamicLookbackOverlap");
    }
}
