using FluentAssertions;
using Indexer.Triggers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.ContentPublisher.ApiCache;
using RedditPodcastPoster.ContentPublisher.Publishers;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class SearchSuggestionsPublishTriggerRules
{
    private readonly AutoMocker _mocker = new();

    public SearchSuggestionsPublishTriggerRules()
    {
        _mocker.Use<IApiCachePurgeCommandBuilder>(new ApiCachePurgeCommandBuilder());
        _mocker.GetMock<ILogger<SearchSuggestionsPublishTrigger>>()
            .Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private Task Run() =>
        _mocker.CreateInstance<SearchSuggestionsPublishTrigger>().Run(new TimerInfo(), CancellationToken.None);

    private void VerifyPurgeLogged(Times times) =>
        _mocker.GetMock<ILogger<SearchSuggestionsPublishTrigger>>().Verify(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(
                        "cf cache purge -z cultpodcasts.com --body '{\\\"files\\\":[\\\"https://api.cultpodcasts.com/search-suggestions\\\"") &&
                    state.ToString()!.Contains("X-Search-Suggestions-Cache") &&
                    state.ToString()!.Contains("does not clear browser caches")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    [Fact(DisplayName =
        "When the weekly publish succeeds, then the search-suggestions purge command is logged at Information (no purge is performed), so it is visible in App Insights.")]
    public async Task Success_LogsPurgeCommand()
    {
        await Run();

        VerifyPurgeLogged(Times.Once());
    }

    [Fact(DisplayName = "When the weekly publish throws, then the failure propagates and no purge command is logged.")]
    public async Task Failure_NoPurgeCommand()
    {
        _mocker.GetMock<ISearchSuggestionsPublisher>()
            .Setup(x => x.PublishSearchSuggestions(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic failure"));

        var act = Run;

        await act.Should().ThrowAsync<InvalidOperationException>();
        VerifyPurgeLogged(Times.Never());
    }
}
