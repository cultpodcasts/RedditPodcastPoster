using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.HomePage;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class HomepagePublisherCatalogueRules
{
    private readonly Mock<IPodcastRepository> _podcasts = new();
    private readonly Mock<IEpisodeRepository> _episodes = new();
    private readonly Mock<ITextSanitiser> _textSanitiser = new();
    private readonly Mock<ISubjectsProvider> _subjects = new();
    private readonly Mock<IAmazonS3> _s3 = new();
    private readonly Mock<ILookupRepository> _lookup = new();
    private readonly Mock<IHomepageCataloguePlayables> _catalogue = new();
    private readonly List<Regex?> _titleRegexes = [];
    private string? _publishedJson;

    private readonly Guid _podcastId = Guid.NewGuid();
    private readonly Episode _podcastEpisode;

    public HomepagePublisherCatalogueRules()
    {
        _podcastEpisode = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = _podcastId,
            PodcastName = "Synthetic Podcast",
            Title = "Podcast episode",
            Description = "Podcast description",
            Length = TimeSpan.FromMinutes(30)
        };
        _podcastEpisode.SetRelease(CatalogueRelease.FromDateTimeUtc(DateTime.UtcNow.AddDays(-2)));

        _subjects.Setup(x => x.GetAll()).Returns(Array.Empty<Subject>().ToAsyncEnumerable());
        _textSanitiser
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .Callback((string _, Regex? regex, string[] _, string[] _, string? _) => _titleRegexes.Add(regex))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _) => title);
        _textSanitiser
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
        _s3
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .Callback((PutObjectRequest request, CancellationToken _) => _publishedJson = request.ContentBody)
            .ReturnsAsync(new PutObjectResponse());
        _lookup.Setup(x => x.GetHomePageCache()).ReturnsAsync((HomePageCache?)null);

        // One recent podcast (the publisher's private projection type), with one recent episode.
        _podcasts
            .Setup(x => x.GetAllBy(
                It.IsAny<Expression<Func<Podcast, bool>>>(),
                It.IsAny<Expression<Func<Podcast, It.IsAnyType>>>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var entryType = invocation.Method.GetGenericArguments()[0];
                var entry = Activator.CreateInstance(entryType)!;
                entryType.GetProperty("Id")!.SetValue(entry, _podcastId);
                entryType.GetProperty("Name")!.SetValue(entry, "Synthetic Podcast");
                var array = Array.CreateInstance(entryType, 1);
                array.SetValue(entry, 0);
                return typeof(AsyncEnumerable)
                    .GetMethods()
                    .Single(m => m.Name == nameof(AsyncEnumerable.ToAsyncEnumerable) &&
                                 m.GetParameters()[0].ParameterType.IsGenericType &&
                                 m.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .MakeGenericMethod(entryType)
                    .Invoke(null, [array])!;
            }));
        _episodes
            .Setup(x => x.GetByPodcastId(_podcastId, It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns(() => new[] { _podcastEpisode }.ToAsyncEnumerable());
        _episodes
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>(), It.IsAny<Expression<Func<Episode, TimeSpan>>>()))
            .Returns(new[] { TimeSpan.FromHours(1), TimeSpan.FromHours(2) }.ToAsyncEnumerable());
        _episodes
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Episode, bool>>>(), It.IsAny<Expression<Func<Episode, Guid>>>()))
            .Returns(new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }.ToAsyncEnumerable());
        _catalogue.Setup(x => x.GetActiveDurations(It.IsAny<CancellationToken>()))
            .ReturnsAsync([TimeSpan.FromHours(3)]);
        _catalogue.Setup(x => x.CountActive(It.IsAny<CancellationToken>())).ReturnsAsync(4);
    }

    private HomepagePublisher Sut() => new(
        _podcasts.Object,
        _episodes.Object,
        _textSanitiser.Object,
        _subjects.Object,
        _s3.Object,
        Options.Create(new ContentOptions
        {
            BucketName = "synthetic-bucket",
            HomepageKey = "homepage",
            PreProcessedHomepageKey = "homepage-ssr",
            SubjectsKey = "subjects",
            PeopleKey = "people",
            FlairsKey = "flairs",
            LanguagesKey = "languages",
            DiscoveryInfoKey = "discovery-info",
            SearchSuggestionsKey = "search-suggestions"
        }),
        _lookup.Object,
        _catalogue.Object,
        NullLogger<HomepagePublisher>.Instance);

    private void GivenCatalogue(params HomepageCataloguePlayable[] items) =>
        _catalogue.Setup(x => x.GetRecent(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(items);

    private static HomepageCataloguePlayable Item(string kind, string parent, string title, DateTime release) => new()
    {
        ContentKind = kind,
        Id = Guid.NewGuid(),
        ParentName = parent,
        Title = title,
        Description = title + " description",
        Release = release,
        Length = TimeSpan.FromMinutes(5),
        Subjects = ["Synthetic Subject"],
        TitleRegex = kind == "NewsReport" ? "^Synthetic " : string.Empty
    };

    private JsonElement[] PublishedEpisodes()
    {
        _publishedJson.Should().NotBeNull();
        return JsonDocument.Parse(_publishedJson!).RootElement.GetProperty("recentEpisodes").EnumerateArray().ToArray();
    }

    [Fact(DisplayName =
        "Homepage: recent news reports, TV show episodes and films are published alongside podcast episodes, " +
        "newest first, each with contentKind, its GUID id and the parent's name in podcastName.")]
    public async Task catalogue_playables_are_published_with_content_kind()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var news = Item("NewsReport", "Synthetic Desk", "News item", now.AddHours(-1));
        var tv = Item("TvShowEpisode", "Synthetic Show", "TV item", now.AddDays(-3));
        var film = Item("Film", "Synthetic Film", "Synthetic Film", now.AddDays(-1));
        GivenCatalogue(news, tv, film);

        // Act
        var result = await Sut().PublishHomepage();

        // Assert
        result.HomepagePublished.Should().BeTrue();
        var episodes = PublishedEpisodes();
        episodes.Select(x => x.GetProperty("episodeTitle").GetString())
            .Should().Equal("News item", "Synthetic Film", "Podcast episode", "TV item");
        episodes[0].GetProperty("contentKind").GetString().Should().Be("NewsReport");
        episodes[0].GetProperty("id").GetGuid().Should().Be(news.Id);
        episodes[0].GetProperty("podcastName").GetString().Should().Be("Synthetic Desk");
        episodes[0].GetProperty("subjects").EnumerateArray().Select(x => x.GetString()).Should().Equal("Synthetic Subject");
        episodes[1].GetProperty("contentKind").GetString().Should().Be("Film");
        episodes[1].GetProperty("podcastName").GetString().Should().Be("Synthetic Film");
        episodes[3].GetProperty("contentKind").GetString().Should().Be("TvShowEpisode");
        episodes[3].GetProperty("podcastName").GetString().Should().Be("Synthetic Show");
    }

    [Fact(DisplayName =
        "Homepage: a podcast episode has no contentKind key at all, so podcast rows are unchanged and an absent " +
        "contentKind still means a podcast episode to older clients.")]
    public async Task podcast_episode_omits_content_kind()
    {
        // Arrange
        GivenCatalogue();

        // Act
        await Sut().PublishHomepage();

        // Assert
        var podcastRow = PublishedEpisodes().Should().ContainSingle().Subject;
        podcastRow.GetProperty("podcastName").GetString().Should().Be("Synthetic Podcast");
        podcastRow.TryGetProperty("contentKind", out _).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Homepage: a news report's title is sanitised with its news organisation's title regex, the same way a " +
        "podcast episode uses its podcast's.")]
    public async Task catalogue_title_uses_parent_title_regex()
    {
        // Arrange
        GivenCatalogue(Item("NewsReport", "Synthetic Desk", "Synthetic headline", DateTime.UtcNow.AddHours(-1)));

        // Act
        await Sut().PublishHomepage();

        // Assert
        _titleRegexes.Should().Contain(x => x != null && x.ToString() == "^Synthetic ");
    }

    [Fact(DisplayName =
        "Homepage: when the episode count and total duration refresh, they include news reports, TV show " +
        "episodes and films as well as podcast episodes, and the sum is saved to the homepage cache.")]
    public async Task refreshed_totals_include_catalogue_playables()
    {
        // Arrange
        GivenCatalogue();
        HomePageCache? saved = null;
        _lookup.Setup(x => x.SaveHomePageCache(It.IsAny<HomePageCache>()))
            .Callback((HomePageCache cache) => saved = cache)
            .Returns(Task.CompletedTask);

        // Act
        await Sut().PublishHomepage();

        // Assert
        var root = JsonDocument.Parse(_publishedJson!).RootElement;
        root.GetProperty("episodeCount").GetInt32().Should().Be(7);
        TimeSpan.Parse(root.GetProperty("totalDuration").GetString()!).Should().Be(TimeSpan.FromHours(6));
        saved.Should().NotBeNull();
        saved!.ActiveEpisodeCount.Should().Be(7);
        saved.TotalDuration.Should().Be(TimeSpan.FromHours(6));
    }

    [Fact(DisplayName =
        "Homepage: when the cached totals are fresh and it is not the weekly refresh window, the catalogue " +
        "totals are not re-read, so a normal publish stays cheap.")]
    public async Task cached_totals_skip_catalogue_scans()
    {
        // Arrange
        GivenCatalogue();
        _lookup.Setup(x => x.GetHomePageCache())
            .ReturnsAsync(new HomePageCache { ActiveEpisodeCount = 10, TotalDuration = TimeSpan.FromHours(9) });
        var utcNow = DateTime.UtcNow;
        if (utcNow is { DayOfWeek: DayOfWeek.Monday, Hour: 0, Minute: < 20 })
        {
            return; // the weekly refresh window always re-reads; nothing to assert then.
        }

        // Act
        await Sut().PublishHomepage();

        // Assert
        _catalogue.Verify(x => x.CountActive(It.IsAny<CancellationToken>()), Times.Never);
        _catalogue.Verify(x => x.GetActiveDurations(It.IsAny<CancellationToken>()), Times.Never);
        JsonDocument.Parse(_publishedJson!).RootElement.GetProperty("episodeCount").GetInt32().Should().Be(10);
    }

    [Fact(DisplayName =
        "Homepage: a catalogue playable becomes a PodcastResult with its kind, parent name, regexes and known " +
        "terms, and no subjects array when it has none.")]
    public void to_podcast_result_carries_kind_and_parent()
    {
        // Arrange
        var item = Item("TvShowEpisode", "Synthetic Show", "Synthetic episode", DateTime.UtcNow) with
        {
            Subjects = [],
            KnownTerms = ["SyntheticTerm"],
            DescriptionRegex = "^(?<body>.*)$"
        };

        // Act
        PodcastResult result = HomepagePublisher.ToPodcastResult(item);

        // Assert
        result.ContentKind.Should().Be("TvShowEpisode");
        result.PodcastName.Should().Be("Synthetic Show");
        result.EpisodeId.Should().Be(item.Id);
        result.DescriptionRegex.Should().Be("^(?<body>.*)$");
        result.KnownTerms.Should().Equal("SyntheticTerm");
        result.Subjects.Should().BeNull();
        HomepagePublisher.ToRecentEpisode(result).ContentKind.Should().Be("TvShowEpisode");
    }
}
