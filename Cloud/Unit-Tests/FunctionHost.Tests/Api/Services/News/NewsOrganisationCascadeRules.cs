using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

public class NewsOrganisationCascadeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<NewsReport> _reports = [];
    private readonly List<NewsReport[]> _saveBatches = [];
    private NewsOrganisation? _saved;

    public NewsOrganisationCascadeRules()
    {
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(organisation => _saved = organisation)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns(() => _reports.ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback<IEnumerable<NewsReport>>(reports => _saveBatches.Add(reports.ToArray()))
            .Returns(Task.CompletedTask);
    }

    private NewsOrganisation CreateOrganisation(bool? removed = null)
    {
        var organisation = new NewsOrganisation("Synthetic Evening News") { Id = _fixture.CreateGuid(), Removed = removed };
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id))
            .ReturnsAsync(organisation);
        return organisation;
    }

    private NewsReport AddReport(NewsOrganisation organisation, bool removed = false)
    {
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = removed };
        report.SetNewsOrganisationProperties(organisation);
        _reports.Add(report);
        return report;
    }

    private Task<NewsOrganisationUpdateResult> Update(NewsOrganisation organisation, NewsOrganisationChangeRequest change) =>
        _mocker.CreateInstance<NewsOrganisationService>()
            .UpdateAsync(new NewsOrganisationChangeRequestWrapper(organisation.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH news organisation removed=true sets parentRemoved on every report and deletes all their search documents, because that is how a podcast removal cascades.")]
    public async Task remove_cascades_to_reports()
    {
        // Arrange
        var organisation = CreateOrganisation();
        var live = AddReport(organisation);
        var alreadyRemoved = AddReport(organisation, removed: true);

        // Act
        var result = await Update(organisation, new NewsOrganisationChangeRequest { Removed = true });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _saved!.IsRemoved().Should().BeTrue();
        live.ParentRemoved.Should().BeTrue();
        alreadyRemoved.ParentRemoved.Should().BeTrue();
        live.Removed.Should().BeFalse();
        alreadyRemoved.Removed.Should().BeTrue();
        _saveBatches.SelectMany(b => b).Should().BeEquivalentTo([live, alreadyRemoved]);
        _search.DeletedKeys.Should().BeEquivalentTo([live.Id.ToString(), alreadyRemoved.Id.ToString()]);
    }

    [Fact(DisplayName =
        "PATCH news organisation removed=false clears parentRemoved and saves the reports but keeps individually removed ones removed and deletes nothing, because the indexer re-adds live reports on its next run.")]
    public async Task unremove_clears_parent_removed_only()
    {
        // Arrange
        var organisation = CreateOrganisation(removed: true);
        var live = AddReport(organisation);
        var individuallyRemoved = AddReport(organisation, removed: true);

        // Act
        await Update(organisation, new NewsOrganisationChangeRequest { Removed = false });

        // Assert
        _saved!.IsRemoved().Should().BeFalse();
        live.ParentRemoved.Should().BeFalse();
        individuallyRemoved.ParentRemoved.Should().BeFalse();
        individuallyRemoved.Removed.Should().BeTrue();
        _saveBatches.SelectMany(b => b).Should().BeEquivalentTo([live, individuallyRemoved]);
        _search.DeleteBatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Retrying PATCH news organisation removed=true saves no unchanged reports but deletes the search documents again, because a retry must finish a partial cascade without rewriting everything.")]
    public async Task remove_retry_is_idempotent()
    {
        // Arrange
        var organisation = CreateOrganisation(removed: true);
        var report = AddReport(organisation);

        // Act
        await Update(organisation, new NewsOrganisationChangeRequest { Removed = true });

        // Assert
        report.ParentRemoved.Should().BeTrue();
        _saveBatches.Should().BeEmpty();
        _search.DeletedKeys.Should().Equal(report.Id.ToString());
    }

    [Fact(DisplayName =
        "PATCH news organisation removed=true over 1001 reports saves in batches of 100 and deletes in batches of 1000, because a large organisation must not exceed the store or Azure Search batch limits.")]
    public async Task remove_batches_large_organisations()
    {
        // Arrange
        var organisation = CreateOrganisation();
        for (var i = 0; i < 1001; i++)
        {
            AddReport(organisation);
        }

        // Act
        await Update(organisation, new NewsOrganisationChangeRequest { Removed = true });

        // Assert
        _saveBatches.Should().HaveCount(11);
        _saveBatches.Max(b => b.Length).Should().Be(100);
        _search.DeleteBatches.Select(b => b.Length).Should().Equal(1000, 1);
        _search.DeletedKeys.Should().OnlyHaveUniqueItems().And.HaveCount(1001);
    }

    [Fact(DisplayName =
        "PATCH news organisation sets publisher (trimmed) and description and GET returns them, because both are now curator-editable.")]
    public async Task update_sets_publisher_and_description()
    {
        // Arrange
        var organisation = CreateOrganisation();

        // Act
        await Update(organisation, new NewsOrganisationChangeRequest
        {
            PublisherName = " Synthetic Broadcasting Corp ",
            Description = "Synthetic evening bulletin"
        });
        var dto = _saved!.ToDto();

        // Assert
        _saved!.PublisherName.Should().Be("Synthetic Broadcasting Corp");
        _saved.Description.Should().Be("Synthetic evening bulletin");
        dto.PublisherName.Should().Be("Synthetic Broadcasting Corp");
        dto.Description.Should().Be("Synthetic evening bulletin");
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()), Times.Never);
    }
}
