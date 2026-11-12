using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

/// <summary>#1397: retiring an empty news organisation by deleting it, never one that still has reports.</summary>
public class NewsOrganisationDeleteServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<Guid> _deleted = [];
    private readonly List<(string FileKey, Guid OwnerId)> _released = [];
    private int _reportCount;

    public NewsOrganisationDeleteServiceRules()
    {
        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(item => item.Id == id));
        organisations
            .Setup(r => r.Delete(It.IsAny<Guid>()))
            .Callback<Guid>(id => _deleted.Add(id))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync(() => _reportCount);
        _mocker.GetMock<IFileKeyAllocator>()
            .Setup(a => a.Release(It.IsAny<string>(), It.IsAny<Guid>()))
            .Callback<string, Guid>((fileKey, ownerId) => _released.Add((fileKey, ownerId)))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ILogger<NewsOrganisationDeleteService>>();
    }

    private NewsOrganisation SeedOrganisation()
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _organisations.Add(organisation);
        return organisation;
    }

    private Task<NewsOrganisationDeleteResult> Delete(Guid id) =>
        _mocker.CreateInstance<NewsOrganisationDeleteService>().DeleteAsync(id, CancellationToken.None);

    [Fact(DisplayName =
        "An organisation with no reports is deleted and its file-key claim released, so its name no longer makes name routes ambiguous and the key can be reused.")]
    public async Task empty_organisation_is_deleted_and_its_file_key_released()
    {
        // Arrange
        var organisation = SeedOrganisation();

        // Act
        var result = await Delete(organisation.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.Deleted);
        _deleted.Should().Equal(organisation.Id);
        _released.Should().Equal((organisation.FileKey, organisation.Id));
    }

    [Fact(DisplayName =
        "An organisation that still has reports (removed ones count) is not deleted and the remaining count is returned, so no report is ever orphaned.")]
    public async Task organisation_with_reports_is_not_deleted()
    {
        // Arrange
        var organisation = SeedOrganisation();
        _reportCount = _fixture.Create<int>() % 50 + 1;

        // Act
        var result = await Delete(organisation.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.HasReports);
        result.RemainingReportCount.Should().Be(_reportCount);
        _deleted.Should().BeEmpty();
        _released.Should().BeEmpty();
    }

    [Fact(DisplayName = "An unknown organisation id is NotFound and nothing is deleted.")]
    public async Task unknown_organisation_is_not_found()
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var result = await Delete(id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.NotFound);
        _deleted.Should().BeEmpty();
    }
}
