using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;

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
    [InlineData(2, "1")]
    [InlineData(3, "1")]
    [InlineData(4, "1")]
    [InlineData(5, "1")]
    [InlineData(6, "1")]
    [InlineData(7, "1")]
    [InlineData(8, "1")]
    [InlineData(9, "1")]
    [InlineData(10, "1")]
    [InlineData(11, "1")]
    [InlineData(12, "2")]
    [InlineData(13, "2")]
    [InlineData(14, "2")]
    [InlineData(15, "2")]
    [InlineData(16, "2")]
    [InlineData(17, "2")]
    [InlineData(18, "2")]
    [InlineData(19, "2")]
    [InlineData(20, "2")]
    [InlineData(21, "2")]
    [InlineData(22, "2")]
    [InlineData(23, "2")]
    public void GetApplication_WithTwoApplications_IsCorrect(int hour, string applicationName)
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
    [InlineData(1, "1")]
    [InlineData(2, "1")]
    [InlineData(3, "1")]
    [InlineData(4, "1")]
    [InlineData(5, "1")]
    [InlineData(6, "2")]
    [InlineData(7, "2")]
    [InlineData(8, "2")]
    [InlineData(9, "2")]
    [InlineData(10, "2")]
    [InlineData(11, "2")]
    [InlineData(12, "3")]
    [InlineData(13, "3")]
    [InlineData(14, "3")]
    [InlineData(15, "3")]
    [InlineData(16, "3")]
    [InlineData(17, "3")]
    [InlineData(18, "4")]
    [InlineData(19, "4")]
    [InlineData(20, "4")]
    [InlineData(21, "4")]
    [InlineData(22, "4")]
    [InlineData(23, "4")]
    public void GetApplication_WithFourApplications_IsCorrect(int hour, string applicationName)
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
    public void GetApplication_WithCombinationUsageFlags_IsCorrect()
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
                    Usage = ApplicationUsage.Discover | ApplicationUsage.Api, DisplayName = _fixture.Create<string>()
                },
                new Application
                {
                    ApiKey = _fixture.Create<string>(), Name = applicationName,
                    Usage = ApplicationUsage.Indexer | ApplicationUsage.Api, DisplayName = _fixture.Create<string>()
                }
            ]
        }));
        // act
        var result = Sut.GetApplication(ApplicationUsage.Indexer);
        // assert
        result.Application.Name.Should().Be(applicationName);
    }
}