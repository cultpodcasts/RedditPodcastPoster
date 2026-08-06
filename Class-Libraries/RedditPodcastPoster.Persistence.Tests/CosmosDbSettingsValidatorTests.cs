using FluentAssertions;
using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.Persistence.Validators;

namespace RedditPodcastPoster.Persistence.Tests;

public class CosmosDbSettingsValidatorTests
{
    private readonly CosmosDbSettingsValidator _validator = new();

    [Fact(DisplayName = "Succeeds when all required Cosmos DB settings are set")]
    public void Succeeds_when_all_required_values_are_set()
    {
        // Arrange
        var options = ValidOptions();

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact(DisplayName = "Fails when Endpoint is blank")]
    public void Fails_when_Endpoint_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.Endpoint = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Endpoint");
    }

    [Fact(DisplayName = "Fails when AuthKeyOrResourceToken is blank")]
    public void Fails_when_AuthKeyOrResourceToken_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.AuthKeyOrResourceToken = "";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("AuthKeyOrResourceToken");
    }

    [Fact(DisplayName = "Fails when DatabaseId is blank")]
    public void Fails_when_DatabaseId_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.DatabaseId = "   ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DatabaseId");
    }

    [Fact(DisplayName = "Fails when PodcastsContainer is blank")]
    public void Fails_when_PodcastsContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.PodcastsContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PodcastsContainer");
    }

    [Fact(DisplayName = "Fails when EpisodesContainer is blank")]
    public void Fails_when_EpisodesContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.EpisodesContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("EpisodesContainer");
    }

    [Fact(DisplayName = "Fails when SubjectsContainer is blank")]
    public void Fails_when_SubjectsContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.SubjectsContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("SubjectsContainer");
    }

    [Fact(DisplayName = "Fails when PeopleContainer is blank")]
    public void Fails_when_PeopleContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.PeopleContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("PeopleContainer");
    }

    [Fact(DisplayName = "Fails when ActivitiesContainer is blank")]
    public void Fails_when_ActivitiesContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.ActivitiesContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("ActivitiesContainer");
    }

    [Fact(DisplayName = "Fails when DiscoveryContainer is blank")]
    public void Fails_when_DiscoveryContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.DiscoveryContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("DiscoveryContainer");
    }

    [Fact(DisplayName = "Fails when LookUpsContainer is blank")]
    public void Fails_when_LookUpsContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.LookUpsContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("LookUpsContainer");
    }

    [Fact(DisplayName = "Fails when TitleCasingRulesContainer is blank")]
    public void Fails_when_TitleCasingRulesContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.TitleCasingRulesContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("TitleCasingRulesContainer");
    }

    [Fact(DisplayName = "Fails when PushSubscriptionsContainer is blank")]
    public void Fails_when_PushSubscriptionsContainer_is_blank()
    {
        // Arrange
        var options = ValidOptions();
        options.PushSubscriptionsContainer = " ";

        // Act
        var result = _validator.Validate(null, options);

        // Assert
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
        TitleCasingRulesContainer = "titlecasingrules",
        PushSubscriptionsContainer = "push"
    };
}
