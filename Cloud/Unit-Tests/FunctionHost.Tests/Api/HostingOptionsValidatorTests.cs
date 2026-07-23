using Api.Configuration;
using Api.Services;
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests.Api;

public class HostingOptionsValidatorTests
{
    private readonly HostingOptionsValidator _validator = new();

    [Fact]
    public void Succeeds_when_UserRoles_empty()
    {
        var options = new HostingOptions { UserRoles = [] };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Succeeds_when_UserRoles_have_values()
    {
        var options = new HostingOptions { UserRoles = ["curate", "admin"] };

        _validator.Validate(null, options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_UserRoles_contains_blank()
    {
        var options = new HostingOptions { UserRoles = ["curate", " "] };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("UserRoles");
    }
}
