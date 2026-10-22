using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.ContentPublisher.ApiCache;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace PublishR2.Tests;

public class R2PublishProcessorRules
{
    private readonly AutoMocker _mocker = new();

    public R2PublishProcessorRules()
    {
        _mocker.Use<IApiCachePurgeCommandBuilder>(new ApiCachePurgeCommandBuilder());
        _mocker.GetMock<ILanguagesPublisher>().Setup(x => x.PublishLanguages()).ReturnsAsync(true);
        GivenHomepagePublished(true);
    }

    private R2PublishProcessor Sut() => _mocker.CreateInstance<R2PublishProcessor>();

    private void GivenHomepagePublished(bool published) =>
        _mocker.GetMock<IHomepagePublisher>().Setup(x => x.PublishHomepage())
            .ReturnsAsync(new PublishHomepageResult(published));

    private void GivenSearchSuggestionsFail() =>
        _mocker.GetMock<ISearchSuggestionsPublisher>()
            .Setup(x => x.PublishSearchSuggestions(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("synthetic failure"));

    private Task<R2PublishResult> Run(R2PublishTarget target) =>
        Sut().Process(new R2PublishRequest { Target = target });

    [Theory(DisplayName =
        "When only non-Api-cached targets are published, then no purge command is produced, because those routes stream from R2 uncached.")]
    [InlineData(R2PublishTarget.Languages)]
    [InlineData(R2PublishTarget.People)]
    [InlineData(R2PublishTarget.Subjects)]
    public async Task NonCachedTargets_NoPurge(R2PublishTarget target)
    {
        var result = await Run(target);

        result.Success.Should().BeTrue();
        result.ApiCachePurge.Should().BeNull();
    }

    [Fact(DisplayName = "When search-suggestions publishes, then only the /search-suggestions URLs are in the purge.")]
    public async Task SearchSuggestions_PurgesSearchSuggestionsOnly()
    {
        var result = await Run(R2PublishTarget.SearchSuggestions);

        result.Success.Should().BeTrue();
        result.ApiCachePurge!.Urls.Should().OnlyContain(u => u.EndsWith("/search-suggestions")).And.HaveCount(2);
    }

    [Fact(DisplayName = "When the homepage publishes, then only the /homepage URLs are in the purge.")]
    public async Task Homepage_PurgesHomepageOnly()
    {
        var result = await Run(R2PublishTarget.Homepage);

        result.Success.Should().BeTrue();
        result.ApiCachePurge!.Urls.Should().OnlyContain(u => u.EndsWith("/homepage")).And.HaveCount(2);
    }

    [Fact(DisplayName = "When lookups runs, then only search-suggestions is purged, because lookups never publishes the homepage.")]
    public async Task Lookups_PurgesSearchSuggestionsOnly()
    {
        var result = await Run(R2PublishTarget.Lookups);

        result.ApiCachePurge!.Urls.Should().OnlyContain(u => u.EndsWith("/search-suggestions"));
        _mocker.GetMock<IHomepagePublisher>().Verify(x => x.PublishHomepage(), Times.Never);
    }

    [Fact(DisplayName = "When all runs successfully, then both /homepage and /search-suggestions are purged.")]
    public async Task All_PurgesBoth()
    {
        var result = await Run(R2PublishTarget.All);

        result.Success.Should().BeTrue();
        result.ApiCachePurge!.Urls.Should().HaveCount(4)
            .And.Contain(u => u.EndsWith("/homepage"))
            .And.Contain(u => u.EndsWith("/search-suggestions"));
    }

    [Fact(DisplayName = "When search-suggestions fails, then the run fails and nothing is purged.")]
    public async Task SearchSuggestionsFails_NoPurge()
    {
        GivenSearchSuggestionsFail();

        var result = await Run(R2PublishTarget.SearchSuggestions);

        result.Success.Should().BeFalse();
        result.ApiCachePurge.Should().BeNull();
    }

    [Fact(DisplayName =
        "When all runs and the homepage fails after search-suggestions succeeded, then the run fails but search-suggestions is still purged, because that new R2 object is live.")]
    public async Task All_HomepageFails_PurgesSearchSuggestionsOnly()
    {
        GivenHomepagePublished(false);

        var result = await Run(R2PublishTarget.All);

        result.Success.Should().BeFalse();
        result.ApiCachePurge!.Urls.Should().OnlyContain(u => u.EndsWith("/search-suggestions"));
    }

    [Fact(DisplayName = "When languages fails during all, then nothing else is published and nothing is purged.")]
    public async Task All_LanguagesFails_NoPurge()
    {
        _mocker.GetMock<ILanguagesPublisher>().Setup(x => x.PublishLanguages()).ReturnsAsync(false);

        var result = await Run(R2PublishTarget.All);

        result.Success.Should().BeFalse();
        result.ApiCachePurge.Should().BeNull();
    }

    [Fact(DisplayName =
        "When lookups throws in subjects after search-suggestions published, then the run fails but the /search-suggestions purge is still returned, because that new R2 object is live.")]
    public async Task Lookups_SubjectsThrows_StillPurgesSearchSuggestions()
    {
        _mocker.GetMock<ISubjectsPublisher>().Setup(x => x.PublishSubjects())
            .ThrowsAsync(new InvalidOperationException("synthetic subjects failure"));

        var result = await Run(R2PublishTarget.Lookups);

        result.Success.Should().BeFalse();
        result.ApiCachePurge!.Urls.Should().OnlyContain(u => u.EndsWith("/search-suggestions")).And.HaveCount(2);
        _mocker.GetMock<ISearchSuggestionsPublisher>()
            .Verify(x => x.PublishSearchSuggestions(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When all throws while building the homepage after search-suggestions published, then the run fails but only the /search-suggestions purge is returned.")]
    public async Task All_HomepageThrows_StillPurgesSearchSuggestions()
    {
        _mocker.GetMock<IHomepagePublisher>().Setup(x => x.PublishHomepage())
            .ThrowsAsync(new InvalidOperationException("synthetic homepage model failure"));

        var result = await Run(R2PublishTarget.All);

        result.Success.Should().BeFalse();
        result.ApiCachePurge!.Urls.Should().OnlyContain(u => u.EndsWith("/search-suggestions")).And.HaveCount(2);
    }

    [Fact(DisplayName = "When the homepage-only target throws, then the run fails and nothing is purged.")]
    public async Task Homepage_Throws_NoPurge()
    {
        _mocker.GetMock<IHomepagePublisher>().Setup(x => x.PublishHomepage())
            .ThrowsAsync(new InvalidOperationException("synthetic homepage model failure"));

        var result = await Run(R2PublishTarget.Homepage);

        result.Success.Should().BeFalse();
        result.ApiCachePurge.Should().BeNull();
    }
}
