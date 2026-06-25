using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeApiKeyStrategyTests
{
    private readonly Mock<IDateTimeService> _dateTimeService = new();
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public YouTubeApiKeyStrategyTests()
    {
        _mocker.Use(_dateTimeService.Object);
    }

    private IYouTubeApiKeyStrategy Sut => _mocker.CreateInstance<YouTubeApiKeyStrategy>();

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "1")]
    [InlineData(12, "1")]
    [InlineData(23, "1")]
    public void GetApplication_WithTwoIndexerApplications_UsesHourFallbackSpread(int hour, string applicationName)
    {
        // arrange
        _mocker.Use(Options.Create(new YouTubeSettings
        {
            Applications =
            [
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = "1", Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = "2", Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                }
            ]
        }));
        _dateTimeService.Setup(x => x.GetHour()).Returns(hour);
        // act
        var result = Sut.GetApplication(ApplicationUsage.Indexer);
        // assert
        result.Application.Name.Should().Be(applicationName);
    }

    [Theory]
    [InlineData(0, "1")]
    [InlineData(1, "2")]
    [InlineData(2, "3")]
    [InlineData(3, "4")]
    [InlineData(4, "1")]
    [InlineData(5, "2")]
    [InlineData(6, "3")]
    [InlineData(7, "4")]
    [InlineData(8, "1")]
    [InlineData(9, "2")]
    [InlineData(10, "3")]
    [InlineData(11, "4")]
    [InlineData(12, "1")]
    [InlineData(13, "2")]
    [InlineData(14, "3")]
    [InlineData(15, "4")]
    [InlineData(16, "1")]
    [InlineData(17, "2")]
    [InlineData(18, "3")]
    [InlineData(19, "4")]
    [InlineData(20, "1")]
    [InlineData(21, "2")]
    [InlineData(22, "3")]
    [InlineData(23, "4")]
    public void GetApplication_WithFourIndexerApplications_UsesHourModuloSpread(int hour, string applicationName)
    {
        // arrange
        _mocker.Use(Options.Create(new YouTubeSettings
        {
            Applications =
            [
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = "1", Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = "2", Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = "3", Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = "4", Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                }
            ]
        }));
        _dateTimeService.Setup(x => x.GetHour()).Returns(hour);
        // act
        var result = Sut.GetApplication(ApplicationUsage.Indexer);
        // assert
        result.Application.Name.Should().Be(applicationName);
    }

    [Fact]
    public void GetApplication_WithSingularUsageFlags_IsCorrect()
    {
        // arrange
        var applicationName = _fixture.Create<string>();
        _mocker.Use(Options.Create(new YouTubeSettings
        {
            Applications =
            [
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = _fixture.Create<string>(),
                    Usage = ApplicationUsage.Discover, DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = applicationName, Usage = ApplicationUsage.Indexer,
                    DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = _fixture.Create<string>(), Usage = ApplicationUsage.Api,
                    DisplayName = _fixture.Create<string>()
                }
            ]
        }));
        // act
        var result = Sut.GetApplication(ApplicationUsage.Indexer);
        // assert
        result.Application.Name.Should().Be(applicationName);
    }

    [Fact]
    public void GetApplication_WithCombinationUsageFlags_UsesExactIndexerMatch()
    {
        // arrange
        var indexerOnlyName = _fixture.Create<string>();
        _mocker.Use(Options.Create(new YouTubeSettings
        {
            Applications =
            [
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = _fixture.Create<string>(),
                    Usage = ApplicationUsage.Discover | ApplicationUsage.Api, DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = indexerOnlyName,
                    Usage = ApplicationUsage.Indexer, DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = _fixture.Create<string>(),
                    Usage = ApplicationUsage.Indexer | ApplicationUsage.Api, DisplayName = _fixture.Create<string>()
                }
            ]
        }));
        // act
        var result = Sut.GetApplication(ApplicationUsage.Indexer);
        // assert
        result.Application.Name.Should().Be(indexerOnlyName);
    }
}