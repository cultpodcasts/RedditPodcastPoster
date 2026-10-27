using FluentAssertions;
using Moq;
using Moq.AutoMock;
using NewsOrganisationDefaultsBackfill;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace NewsOrganisationDefaultsBackfill.Tests.BusinessRules;

public class NewsOrganisationDefaultsBackfillProcessorRules
{
    private readonly AutoMocker _mocker = new();

    private static NewsOrganisation Organisation(CatalogueIndexingSource? indexing) =>
        new($"Synthetic News {Guid.NewGuid():N}") { Id = Guid.NewGuid(), Indexing = indexing };

    private static CatalogueIndexingSource Flags(bool? bypass, bool? hero) =>
        new() { BypassShortEpisodeChecking = bypass, AlwaysPromoteAsHero = hero, IndexAllEpisodes = true };

    private void Store(params NewsOrganisation[] organisations)
    {
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAll())
            .Returns(organisations.ToAsyncEnumerable());
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.PatchCreationDefaults(It.IsAny<Guid>(), It.IsAny<bool>()))
            .ReturnsAsync(NewsOrganisationDefaultsPatchResult.Patched);
    }

    [Fact(DisplayName =
        "News defaults backfill: a dry run counts what it would patch and writes nothing, because the default must be " +
        "safe to run against the live catalogue.")]
    public async Task dry_run_counts_and_writes_nothing()
    {
        // Arrange
        Store(
            Organisation(Flags(true, true)),
            Organisation(null),
            Organisation(Flags(null, null)),
            Organisation(Flags(false, true)));
        var processor = _mocker.CreateInstance<NewsOrganisationDefaultsBackfillProcessor>();

        // Act
        var result = await processor.Run(new NewsOrganisationDefaultsBackfillRequest());

        // Assert
        result.Scanned.Should().Be(4);
        result.AlreadySet.Should().Be(1);
        result.ToPatch.Should().Be(3);
        result.WithoutIndexing.Should().Be(1);
        result.ExplicitFalse.Should().ContainSingle();
        result.Patched.Should().Be(0);
        result.ExitCode.Should().Be(0);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(
            x => x.PatchCreationDefaults(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(x => x.Save(It.IsAny<NewsOrganisation>()), Times.Never);
    }

    [Fact(DisplayName =
        "News defaults backfill: --apply patches every news organisation missing either flag (unset, false or no " +
        "indexing block) with the partial patch and never a whole-document save, because concurrent edits must not " +
        "be overwritten.")]
    public async Task apply_patches_each_one_missing_a_flag()
    {
        // Arrange
        var done = Organisation(Flags(true, true));
        var noIndexing = Organisation(null);
        var unset = Organisation(Flags(null, true));
        var heroOff = Organisation(Flags(true, false));
        Store(done, noIndexing, unset, heroOff);
        var processor = _mocker.CreateInstance<NewsOrganisationDefaultsBackfillProcessor>();

        // Act
        var result = await processor.Run(new NewsOrganisationDefaultsBackfillRequest { Apply = true });

        // Assert
        result.Patched.Should().Be(3);
        var repository = _mocker.GetMock<INewsOrganisationRepository>();
        repository.Verify(x => x.PatchCreationDefaults(noIndexing.Id, false), Times.Once);
        repository.Verify(x => x.PatchCreationDefaults(unset.Id, true), Times.Once);
        repository.Verify(x => x.PatchCreationDefaults(heroOff.Id, true), Times.Once);
        repository.Verify(x => x.PatchCreationDefaults(done.Id, It.IsAny<bool>()), Times.Never);
        repository.Verify(x => x.Save(It.IsAny<NewsOrganisation>()), Times.Never);
    }

    [Fact(DisplayName =
        "News defaults backfill: a re-run after every news organisation has both flags writes nothing, because the " +
        "backfill must be idempotent.")]
    public async Task rerun_is_idempotent()
    {
        // Arrange
        Store(Organisation(Flags(true, true)), Organisation(Flags(true, true)));
        var processor = _mocker.CreateInstance<NewsOrganisationDefaultsBackfillProcessor>();

        // Act
        var result = await processor.Run(new NewsOrganisationDefaultsBackfillRequest { Apply = true });

        // Assert
        result.AlreadySet.Should().Be(2);
        result.ToPatch.Should().Be(0);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(
            x => x.PatchCreationDefaults(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact(DisplayName =
        "News defaults backfill: a document whose indexing block changed since it was read is reported and the run " +
        "exits 2, because nothing was written to it and a re-run is needed.")]
    public async Task shape_change_is_reported_for_rerun()
    {
        // Arrange
        var changed = Organisation(null);
        Store(changed);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.PatchCreationDefaults(changed.Id, false))
            .ReturnsAsync(NewsOrganisationDefaultsPatchResult.ShapeChanged);
        var processor = _mocker.CreateInstance<NewsOrganisationDefaultsBackfillProcessor>();

        // Act
        var result = await processor.Run(new NewsOrganisationDefaultsBackfillRequest { Apply = true });

        // Assert
        result.ShapeChanged.Should().ContainSingle().Which.Should().Contain(changed.Id.ToString());
        result.Patched.Should().Be(0);
        result.ExitCode.Should().Be(2);
    }
}
