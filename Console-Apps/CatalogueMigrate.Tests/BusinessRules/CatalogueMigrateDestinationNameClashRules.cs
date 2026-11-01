using System.Linq.Expressions;
using CatalogueMigrate;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using Xunit;

namespace CatalogueMigrate.Tests.BusinessRules;

/// <summary>
/// #1148: the migrate destination's "another parent already has this name" check runs the real repository query
/// (compiled in memory), so letter case cannot slip a near-duplicate news organisation or TV show past it.
/// </summary>
public class CatalogueMigrateDestinationNameClashRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<TvShow> _tvShows = [];
    private readonly List<NewsOrganisation> _newsOrganisations = [];

    public CatalogueMigrateDestinationNameClashRules()
    {
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) =>
                Task.FromResult(_tvShows.FirstOrDefault(CosmosLinqInMemoryRewriter.ForInMemory(selector).Compile())));
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> selector) =>
                Task.FromResult(
                    _newsOrganisations.FirstOrDefault(CosmosLinqInMemoryRewriter.ForInMemory(selector).Compile())));
    }

    private string CreateFileKey() => "key-" + _fixture.CreateGuid().ToString("N");

    private (string Stored, string Migrated) CreateCaseVariantNames()
    {
        var name = _fixture.CreateTitle();
        var stored = name.ToLowerInvariant();
        var migrated = name.ToUpperInvariant();
        stored.Should().NotBe(migrated, "the synthetic title needs letters to make a case variant");
        return (stored, migrated);
    }

    [Fact(DisplayName =
        "Migrate name clash: a TV show whose stored name differs from the migrated parent's name only in letter case " +
        "is found, because the check compares names ignoring case and must not create a near-duplicate.")]
    public async Task tv_show_case_variant_name_clashes()
    {
        // Arrange
        var (stored, migrated) = CreateCaseVariantNames();
        var existing = new TvShow(stored) { Id = _fixture.CreateGuid(), FileKey = CreateFileKey() };
        _tvShows.Add(existing);
        var reader = _mocker.CreateInstance<RepositoryCatalogueMigrateDestinationReader>();

        // Act
        var clash = await reader.FindOtherParentWithName(
            SubmitClassification.TvShowEpisode, migrated, CreateFileKey(), _fixture.CreateGuid());

        // Assert
        clash.Should().Be(existing.Id);
    }

    [Fact(DisplayName =
        "Migrate name clash: a news organisation whose stored name differs only in letter case is found, because news " +
        "organisations get the same case-insensitive uniqueness as TV shows.")]
    public async Task news_organisation_case_variant_name_clashes()
    {
        // Arrange
        var (stored, migrated) = CreateCaseVariantNames();
        var existing = new NewsOrganisation(stored) { Id = _fixture.CreateGuid(), FileKey = CreateFileKey() };
        _newsOrganisations.Add(existing);
        var reader = _mocker.CreateInstance<RepositoryCatalogueMigrateDestinationReader>();

        // Act
        var clash = await reader.FindOtherParentWithName(
            SubmitClassification.NewsReport, migrated, CreateFileKey(), _fixture.CreateGuid());

        // Assert
        clash.Should().Be(existing.Id);
    }

    [Fact(DisplayName =
        "Migrate name clash: the parent being migrated is never its own clash, even under a different letter case, " +
        "because a resumed migration finds its own earlier write.")]
    public async Task own_parent_is_not_a_clash()
    {
        // Arrange
        var (stored, migrated) = CreateCaseVariantNames();
        var fileKey = CreateFileKey();
        var own = new TvShow(stored) { Id = _fixture.CreateGuid(), FileKey = fileKey };
        _tvShows.Add(own);
        var reader = _mocker.CreateInstance<RepositoryCatalogueMigrateDestinationReader>();

        // Act
        var clash = await reader.FindOtherParentWithName(SubmitClassification.TvShowEpisode, migrated, fileKey, own.Id);

        // Assert
        clash.Should().BeNull();
    }

    [Fact(DisplayName =
        "Migrate name clash: a TV show with a different name and file key is not a clash, because only the same name " +
        "(in any case) or the same file key blocks the migration.")]
    public async Task different_name_and_file_key_is_not_a_clash()
    {
        // Arrange
        _tvShows.Add(new TvShow(_fixture.CreateTitle() + " stored") { Id = _fixture.CreateGuid(), FileKey = CreateFileKey() });
        var reader = _mocker.CreateInstance<RepositoryCatalogueMigrateDestinationReader>();

        // Act
        var clash = await reader.FindOtherParentWithName(
            SubmitClassification.TvShowEpisode, _fixture.CreateTitle() + " migrated", CreateFileKey(), _fixture.CreateGuid());

        // Assert
        clash.Should().BeNull();
    }
}
