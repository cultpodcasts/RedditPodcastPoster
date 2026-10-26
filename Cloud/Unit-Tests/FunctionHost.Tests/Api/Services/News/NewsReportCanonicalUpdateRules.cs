using System.Globalization;
using System.Linq.Expressions;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Episodes;
using Api.Services.News;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

public class NewsReportCanonicalUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private string _organisationName = string.Empty;
    private DateOnly _release;
    private readonly AutoMocker _mocker = new();
    private readonly Mock<SearchClient> _searchClient = new();
    private NewsReport? _saved;

    public NewsReportCanonicalUpdateRules()
    {
        _mocker.Use(NullLogger<NewsReportService>.Instance);
        _searchClient
            .Setup(c => c.DeleteDocumentsAsync(
                It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IndexDocumentsOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                SearchModelFactory.IndexDocumentsResult([SearchModelFactory.IndexingResult("id", null, true, 200)]),
                Mock.Of<Response>()));
        _mocker.Use(new EpisodeSearchIndexCleanup(_searchClient.Object, NullLogger<EpisodeSearchIndexCleanup>.Instance));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(report => _saved = report)
            .Returns(Task.CompletedTask);
    }

    private NewsReport CreateReport(Action<NewsReport>? customise = null)
    {
        _organisationName = _fixture.CreateTitle();
        _release = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(10));
        var organisation = new NewsOrganisation(_organisationName) { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        report.SetNewsOrganisationProperties(organisation);
        report.SetRelease(CatalogueRelease.FromDate(_release));
        report.Subjects = ["Synthetic Cult"];
        customise?.Invoke(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(report);
        return report;
    }

    private Task<NewsReportUpdateResult> Update(NewsReport report, NewsReportChangeRequest change) =>
        _mocker.CreateInstance<NewsReportService>()
            .UpdateAsync(new NewsReportChangeRequestWrapper(report.Id, change), CancellationToken.None);

    private void VerifySearchDelete(NewsReport report, Times times) =>
        _searchClient.Verify(c => c.DeleteDocumentsAsync(
            "id",
            It.Is<IEnumerable<string>>(keys => keys.Single() == report.Id.ToString()),
            It.IsAny<IndexDocumentsOptions>(),
            It.IsAny<CancellationToken>()), times);

    [Fact(DisplayName =
        "PATCH news report sets title, description, release date, duration and flags it is given and leaves omitted fields alone, because omitted JSON is a no-op.")]
    public async Task update_sets_given_fields()
    {
        // Arrange
        var report = CreateReport(r => r.HashTag = "#Keep");
        var release = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(2));
        var duration = _fixture.CreateDuration() + TimeSpan.FromSeconds(30);

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            Title = "  Synthetic raid follow-up  ",
            Description = "Synthetic description.",
            Release = release.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Duration = duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture),
            Explicit = true,
            Ignored = true
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved.Should().BeSameAs(report);
        _saved!.Title.Should().Be("Synthetic raid follow-up");
        _saved.Description.Should().Be("Synthetic description.");
        _saved.Release!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        _saved.Release.Date.Should().Be(release);
        _saved.Length.Should().Be(duration);
        _saved.Explicit.Should().BeTrue();
        _saved.Ignored.Should().BeTrue();
        _saved.HashTag.Should().Be("#Keep");
        _saved.Subjects.Should().Equal("Synthetic Cult");
    }

    [Fact(DisplayName =
        "PATCH news report services adds a link under the key its URL resolves to and removes a key sent with no URL or image, because playables share the film services patch.")]
    public async Task services_add_and_remove()
    {
        // Arrange
        var oldKey = "internetArchive";
        var report = CreateReport(r => r.Services = new Dictionary<string, ServiceLink>
        {
            [oldKey] = new() { Url = new Uri($"https://archive.org/details/synthetic-{_fixture.CreateGuid():N}") }
        });
        var youTube = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["anything"] = new() { Url = youTube },
                [oldKey] = new() { Url = new Uri("", UriKind.RelativeOrAbsolute) }
            }
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved!.Services.Should().ContainKey("youtube");
        _saved.Services!["youtube"].Url.Should().Be(youTube);
        _saved.Services.Should().NotContainKey(oldKey);
    }

    [Fact(DisplayName =
        "PATCH news report rejects a non-absolute services URL without saving, because invalid links must not wipe good ones.")]
    public async Task services_reject_relative_url()
    {
        // Arrange
        var report = CreateReport(r => r.Services = new Dictionary<string, ServiceLink>
        {
            ["youtube"] = new() { Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}") }
        });

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new() { Url = new Uri("/relative", UriKind.Relative) }
            }
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.BadRequest);
        _saved.Should().BeNull();
        report.Services.Should().ContainKey("youtube");
    }

    [Fact(DisplayName =
        "PATCH news report with a release that is not a yyyy-MM-dd date returns BadRequest and saves nothing, because news reports have calendar-date releases.")]
    public async Task update_rejects_a_release_with_a_time()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            Title = "Should not apply", Release = "2026-10-03T12:00:00Z"
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.BadRequest);
        result.Message.Should().Contain("yyyy-MM-dd");
        _saved.Should().BeNull();
        report.Title.Should().NotBe("Should not apply");
    }

    [Fact(DisplayName =
        "PATCH news report with an invalid duration returns BadRequest and saves nothing, because a bad length must not be stored.")]
    public async Task update_rejects_an_invalid_duration()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Duration = "six minutes" });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.BadRequest);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report subjects replaces the curator subjects and remembers dropped ones as removed, because auto-categorisation must not re-add them.")]
    public async Task update_subjects_tracks_removed_subjects()
    {
        // Arrange
        var report = CreateReport();

        // Act
        await Update(report, new NewsReportChangeRequest { Subjects = ["Synthetic Movement"] });

        // Assert
        _saved!.Subjects.Should().Equal("Synthetic Movement");
        _saved.RemovedSubjects.Should().Contain("Synthetic Cult");
    }

    [Fact(DisplayName =
        "PATCH news report with empty hashtag and English language clears both, because empty means unset and null means English.")]
    public async Task update_clears_hashtag_and_normalises_language()
    {
        // Arrange
        var report = CreateReport(r =>
        {
            r.HashTag = "#Old";
            r.Language = "fr";
        });

        // Act
        await Update(report, new NewsReportChangeRequest { HashTag = "", Language = "en" });

        // Assert
        _saved!.HashTag.Should().BeNull();
        _saved.Language.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report guests replaces the list, trimming names and dropping blanks, because guests follow the episode edit API.")]
    public async Task update_replaces_guests()
    {
        // Arrange
        var report = CreateReport(r => r.Guests = ["Synthetic Old Guest"]);

        // Act
        var result = await Update(report, new NewsReportChangeRequest
        {
            Guests = [" Synthetic Reporter ", "", "  ", "Synthetic Witness"]
        });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved!.Guests.Should().Equal("Synthetic Reporter", "Synthetic Witness");
        VerifySearchDelete(report, Times.Never());
    }

    [Fact(DisplayName =
        "PATCH news report with an empty guests array clears guests to null, because [] is how a curator removes every guest on episodes too.")]
    public async Task update_clears_guests_with_empty_array()
    {
        // Arrange
        var report = CreateReport(r => r.Guests = ["Synthetic Old Guest"]);

        // Act
        await Update(report, new NewsReportChangeRequest { Guests = [] });

        // Assert
        _saved!.Guests.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news report without guests leaves existing guests alone, because omitted JSON is a no-op.")]
    public async Task update_without_guests_keeps_guests()
    {
        // Arrange
        var report = CreateReport(r => r.Guests = ["Synthetic Kept Guest"]);

        // Act
        await Update(report, new NewsReportChangeRequest { Title = "Synthetic retitle" });

        // Assert
        _saved!.Guests.Should().Equal("Synthetic Kept Guest");
    }

    [Fact(DisplayName =
        "PATCH news report removed=true saves the report and deletes its search document, because the newsreport search indexer never deletes.")]
    public async Task removing_a_report_deletes_its_search_document()
    {
        // Arrange
        var report = CreateReport();

        // Act
        var result = await Update(report, new NewsReportChangeRequest { Removed = true });

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _saved!.Removed.Should().BeTrue();
        VerifySearchDelete(report, Times.Once());
    }

    [Fact(DisplayName =
        "PATCH news report that does not change removed leaves the search document alone, because only a removal deletes it.")]
    public async Task other_edits_do_not_touch_search()
    {
        // Arrange
        var report = CreateReport();

        // Act
        await Update(report, new NewsReportChangeRequest { Title = "Synthetic retitle" });

        // Assert
        VerifySearchDelete(report, Times.Never());
    }

    [Fact(DisplayName =
        "PATCH news report that does not exist returns NotFound and saves nothing, because there is nothing to edit.")]
    public async Task update_unknown_returns_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.UpdateAsync(
            new NewsReportChangeRequestWrapper(_fixture.CreateGuid(), new NewsReportChangeRequest { Title = "x" }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET news report by id returns Found with its organisation, date-only release, duration and guests, because curator UIs load the report before editing.")]
    public async Task get_by_id_returns_the_report()
    {
        // Arrange
        var report = CreateReport(r => r.Guests = ["Synthetic Reporter"]);
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.GetAsync(report.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsReportGetStatus.Found);
        var dto = result.Report!.ToDto();
        dto.Id.Should().Be(report.Id);
        dto.NewsOrganisationId.Should().Be(report.NewsOrganisationId);
        dto.NewsOrganisationName.Should().Be(_organisationName);
        dto.Release.Should().Be(_release.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        dto.Duration.Should().Be(report.Length.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture));
        dto.Subjects.Should().Equal("Synthetic Cult");
        dto.Guests.Should().Equal("Synthetic Reporter");
    }
}
