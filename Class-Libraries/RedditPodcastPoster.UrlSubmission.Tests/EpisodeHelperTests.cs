using AutoFixture;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests;

public class EpisodeHelperTests
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IEpisodeHelper Sut => _mocker.CreateInstance<EpisodeHelper>();

    [Fact(DisplayName =
        "When the stored episode title contains the resolved Spotify title and Spotify is already assigned, " +
        "the episode is not treated as a title match.")]
    public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndAlreadySpotifyAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, "prefix " + substring + " suffix")
            .With(x => x.Ids, new EpisodeIds { Spotify = "spotifyid" })
            .With(x => x.Services, new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = new Uri("http://existing-url") }
            })
            .Create();
        var spotifyItem = _fixture.Build<CategorisedSpotifyItem>()
            .With(x => x.EpisodeTitle, substring)
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.Spotify)
            .With(x => x.ResolvedSpotifyItem, spotifyItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedYouTubeItem, (CategorisedYouTubeItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the stored episode title contains the resolved Spotify title and Spotify is not assigned, " +
        "the episode matches so the missing Spotify identity can be applied.")]
    public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndNotAlreadySpotifyAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, "prefix " + substring + " suffix")
            .Without(x => x.Ids)
            .Without(x => x.Services)
            .Create();
        var spotifyItem = _fixture.Build<CategorisedSpotifyItem>()
            .With(x => x.EpisodeTitle, substring)
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.Spotify)
            .With(x => x.ResolvedSpotifyItem, spotifyItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedYouTubeItem, (CategorisedYouTubeItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeTrue();
    }


    [Fact(DisplayName =
        "When the resolved Spotify title contains the stored episode title and Spotify is already assigned, " +
        "the episode is not treated as a title match.")]
    public void IsMatchingEpisode_WhenContainsEpisodeNameAndAlreadySpotifyAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, substring)
            .With(x => x.Ids, new EpisodeIds { Spotify = "spotifyid" })
            .With(x => x.Services, new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = new Uri("http://existing-url") }
            })
            .Create();
        var spotifyItem = _fixture.Build<CategorisedSpotifyItem>()
            .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.Spotify)
            .With(x => x.ResolvedSpotifyItem, spotifyItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedYouTubeItem, (CategorisedYouTubeItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the resolved Spotify title contains the stored episode title and Spotify is not assigned, " +
        "the episode matches so the missing Spotify identity can be applied.")]
    public void IsMatchingEpisode_WhenContainsEpisodeNameAndNotAlreadySpotifyAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, substring)
            .Without(x => x.Ids)
            .Without(x => x.Services)
            .Create();
        var spotifyItem = _fixture.Build<CategorisedSpotifyItem>()
            .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.Spotify)
            .With(x => x.ResolvedSpotifyItem, spotifyItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedYouTubeItem, (CategorisedYouTubeItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the stored episode title contains the resolved YouTube title and YouTube is already assigned, " +
        "the episode is not treated as a title match.")]
    public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndAlreadyYouTubeAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, "prefix " + substring + " suffix")
            .With(x => x.Ids, new EpisodeIds { YouTube = "youtubeid" })
            .With(x => x.Services, new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri("http://existing-url") }
            })
            .Create();
        var youTubeItem = _fixture.Build<CategorisedYouTubeItem>()
            .With(x => x.EpisodeTitle, substring)
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.YouTube)
            .With(x => x.ResolvedYouTubeItem, youTubeItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedSpotifyItem, (CategorisedSpotifyItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the stored episode title contains the resolved YouTube title and YouTube is not assigned, " +
        "the episode matches so the missing YouTube identity can be applied.")]
    public void IsMatchingEpisode_WhenContainsResolvedEpisodeNameAndNotAlreadyYouTubeAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, "prefix " + substring + " suffix")
            .Without(x => x.Ids)
            .Without(x => x.Services)
            .Create();
        var youTubeItem = _fixture.Build<CategorisedYouTubeItem>()
            .With(x => x.EpisodeTitle, substring)
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.YouTube)
            .With(x => x.ResolvedYouTubeItem, youTubeItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedSpotifyItem, (CategorisedSpotifyItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeTrue();
    }


    [Fact(DisplayName =
        "When the resolved YouTube title contains the stored episode title and YouTube is already assigned, " +
        "the episode is not treated as a title match.")]
    public void IsMatchingEpisode_WhenContainsEpisodeNameAndAlreadyYouTubeAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, substring)
            .With(x => x.Ids, new EpisodeIds { YouTube = "youtubeid" })
            .With(x => x.Services, new Dictionary<string, EpisodeServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri("http://existing-url") }
            })
            .Create();
        var youTubeItem = _fixture.Build<CategorisedYouTubeItem>()
            .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.YouTube)
            .With(x => x.ResolvedYouTubeItem, youTubeItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedSpotifyItem, (CategorisedSpotifyItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the resolved YouTube title contains the stored episode title and YouTube is not assigned, " +
        "the episode matches so the missing YouTube identity can be applied.")]
    public void IsMatchingEpisode_WhenContainsEpisodeNameAndNotAlreadyYouTubeAssigned_IsCorrect()
    {
        // Arrange
        var substring = "component";
        var episode = _fixture.Build<Episode>()
            .With(x => x.Title, substring)
            .Without(x => x.Ids)
            .Without(x => x.Services)
            .Create();
        var youTubeItem = _fixture.Build<CategorisedYouTubeItem>()
            .With(x => x.EpisodeTitle, "prefix " + substring + " suffix")
            .Create();
        var categorisedItem = _fixture.Build<CategorisedItem>()
            .With(x => x.Authority, Service.YouTube)
            .With(x => x.ResolvedYouTubeItem, youTubeItem)
            .With(x => x.ResolvedAppleItem, (CategorisedAppleItem?)null)
            .With(x => x.ResolvedSpotifyItem, (CategorisedSpotifyItem?)null)
            .Create();
        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);
        // Assert
        result.Should().BeTrue();
    }
}