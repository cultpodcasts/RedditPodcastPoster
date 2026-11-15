using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.CloudflareRedirect.Models;
using RedditPodcastPoster.CloudflareRedirect.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

/// <summary>
/// #1397: retiring an empty news organisation by deleting it, never one that still has reports, optionally redirecting
/// its old name route to the organisation its reports went to.
/// </summary>
public class NewsOrganisationDeleteServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<Guid> _deleted = [];
    private readonly List<(string FileKey, Guid OwnerId)> _released = [];
    private readonly List<CatalogueRedirect> _redirects = [];
    private readonly InMemoryParentIndexingLockStore _locks = new();
    private int _reportCount;
    private bool _redirectSafe = true;

    public NewsOrganisationDeleteServiceRules()
    {
        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(item => item.Id == id));
        organisations
            .Setup(r => r.GetAllBy(It.IsAny<System.Linq.Expressions.Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((System.Linq.Expressions.Expression<Func<NewsOrganisation, bool>> selector) =>
                _organisations.Where(selector.Compile()).ToAsyncEnumerable());
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
        var redirects = _mocker.GetMock<IRedirectService>();
        redirects
            .Setup(s => s.IsCatalogueRedirectSafe(It.IsAny<CatalogueRedirect>()))
            .ReturnsAsync(() => _redirectSafe);
        redirects
            .Setup(s => s.CreateCatalogueRedirect(It.IsAny<CatalogueRedirect>()))
            .Callback<CatalogueRedirect>(_redirects.Add)
            .ReturnsAsync(true);
        _mocker.Use<IParentIndexingLockStore>(_locks);
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<ILogger<NewsOrganisationDeleteService>>();
    }

    private NewsOrganisation SeedOrganisation(string? name = null, Action<NewsOrganisation>? configure = null)
    {
        var organisation = new NewsOrganisation(name ?? _fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(organisation);
        _organisations.Add(organisation);
        return organisation;
    }

    private Task<NewsOrganisationDeleteResult> Delete(Guid id, Guid? redirectTo = null) =>
        _mocker.CreateInstance<NewsOrganisationDeleteService>().DeleteAsync(id, redirectTo, CancellationToken.None);

    [Fact(DisplayName =
        "An organisation with no reports is deleted and its file-key claim released, so its name no longer makes name routes ambiguous and the key can be reused; without a redirect target no redirect is written.")]
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
        _redirects.Should().BeEmpty();
        (await _locks.TryGet(organisation.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "An organisation that still has reports (removed ones count) is not deleted and the remaining count is returned, so no report is ever orphaned.")]
    public async Task organisation_with_reports_is_not_deleted()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var target = SeedOrganisation();
        _reportCount = _fixture.Create<int>() % 50 + 1;

        // Act
        var result = await Delete(organisation.Id, target.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.HasReports);
        result.RemainingReportCount.Should().Be(_reportCount);
        _deleted.Should().BeEmpty();
        _released.Should().BeEmpty();
        _redirects.Should().BeEmpty();
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

    [Fact(DisplayName =
        "While the organisation is being indexed (its indexing lease is held) the delete is refused as Busy and nothing is deleted, so the indexer cannot add a report between the count and the delete.")]
    public async Task organisation_being_indexed_is_not_deleted()
    {
        // Arrange
        var organisation = SeedOrganisation();
        await _locks.TryAcquire(organisation.Id, DateTime.UtcNow, TimeSpan.FromMinutes(5), ParentIndexingLock.SourceHourly);

        // Act
        var result = await Delete(organisation.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.Busy);
        _deleted.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "With redirectToNewsOrganisationId and a different target name, a news-organisation redirect from the old name to the target's name is recorded before the organisation is deleted.")]
    public async Task differently_named_target_gets_a_redirect()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var target = SeedOrganisation();

        // Act
        var result = await Delete(organisation.Id, target.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.Deleted);
        result.RedirectedToName.Should().Be(target.Name);
        _redirects.Should().Equal(new CatalogueRedirect(CatalogueRedirectKind.NewsOrganisation, organisation.Name, target.Name));
        _deleted.Should().Equal(organisation.Id);
    }

    [Fact(DisplayName =
        "No redirect is written when the target has the same name (case-insensitive), because the old route already resolves to the target once the duplicate is gone.")]
    public async Task same_named_target_needs_no_redirect()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var organisation = SeedOrganisation(name);
        var target = SeedOrganisation(name.ToUpperInvariant());

        // Act
        var result = await Delete(organisation.Id, target.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.Deleted);
        result.RedirectedToName.Should().BeNull();
        _redirects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "No redirect is written while another news organisation still has the old name (the old URL belongs to it), as in rename; the delete still runs.")]
    public async Task homonym_keeps_the_old_route()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var organisation = SeedOrganisation(name);
        SeedOrganisation(name);
        var target = SeedOrganisation();

        // Act
        var result = await Delete(organisation.Id, target.Id);

        // Assert
        result.Status.Should().Be(NewsOrganisationDeleteStatus.Deleted);
        _redirects.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A redirect target that is missing, removed or the organisation itself, or a redirect the loop guard refuses, is a BadRequest and nothing is deleted or redirected.")]
    public async Task bad_redirect_targets_refuse_the_delete()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var removed = SeedOrganisation(configure: item => item.Removed = true);
        var looping = SeedOrganisation();

        // Act
        var missing = await Delete(organisation.Id, _fixture.CreateGuid());
        var toRemoved = await Delete(organisation.Id, removed.Id);
        var self = await Delete(organisation.Id, organisation.Id);
        _redirectSafe = false;
        var loop = await Delete(organisation.Id, looping.Id);

        // Assert
        new[] { missing, toRemoved, self, loop }.Should()
            .OnlyContain(result => result.Status == NewsOrganisationDeleteStatus.BadRequest && result.Error != null);
        _deleted.Should().BeEmpty();
        _redirects.Should().BeEmpty();
    }
}
