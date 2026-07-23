using FluentAssertions;
using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.Persistence.Validators;

namespace RedditPodcastPoster.Persistence.Tests;

public class CosmosDbSettingsValidatorTests
{
    private readonly CosmosDbSettingsValidator _validator = new();

    [Fact]
    public void Succeeds_when_all_required_values_are_set()
    {
        _validator.Validate(null, ValidOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_Endpoint_is_blank()
    {
        var options = ValidOptions();
        options.Endpoint = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Endpoint");
    }

    [Fact]
    public void Fails_when_AuthKeyOrResourceToken_is_blank()
    {
        var options = ValidOptions();
        options.AuthKeyOrResourceToken = "";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("AuthKeyOrResourceToken");
    }

    [Fact]
    public void Fails_when_DatabaseId_is_blank()
    {
        var options = ValidOptions();
        options.DatabaseId = "   ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DatabaseId");
    }

    [Fact]
    public void Fails_when_PodcastsContainer_is_blank()
    {
        var options = ValidOptions();
        options.PodcastsContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PodcastsContainer");
    }

    [Fact]
    public void Fails_when_EpisodesContainer_is_blank()
    {
        var options = ValidOptions();
        options.EpisodesContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("EpisodesContainer");
    }

    [Fact]
    public void Fails_when_SubjectsContainer_is_blank()
    {
        var options = ValidOptions();
        options.SubjectsContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SubjectsContainer");
    }

    [Fact]
    public void Fails_when_PeopleContainer_is_blank()
    {
        var options = ValidOptions();
        options.PeopleContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PeopleContainer");
    }

    [Fact]
    public void Fails_when_ActivitiesContainer_is_blank()
    {
        var options = ValidOptions();
        options.ActivitiesContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ActivitiesContainer");
    }

    [Fact]
    public void Fails_when_DiscoveryContainer_is_blank()
    {
        var options = ValidOptions();
        options.DiscoveryContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DiscoveryContainer");
    }

    [Fact]
    public void Fails_when_LookUpsContainer_is_blank()
    {
        var options = ValidOptions();
        options.LookUpsContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("LookUpsContainer");
    }

    [Fact]
    public void Fails_when_PushSubscriptionsContainer_is_blank()
    {
        var options = ValidOptions();
        options.PushSubscriptionsContainer = " ";

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PushSubscriptionsContainer");
    }

    private static CosmosDbSettings ValidOptions() => new()
    {
        Endpoint = "https://example.documents.azure.com:443/",
        AuthKeyOrResourceToken = "key",
        DatabaseId = "db",
        PodcastsContainer = "podcasts",
        EpisodesContainer = "episodes",
        SubjectsContainer = "subjects",
        PeopleContainer = "people",
        ActivitiesContainer = "activities",
        DiscoveryContainer = "discovery",
        LookUpsContainer = "lookups",
        PushSubscriptionsContainer = "push"
    };
}
