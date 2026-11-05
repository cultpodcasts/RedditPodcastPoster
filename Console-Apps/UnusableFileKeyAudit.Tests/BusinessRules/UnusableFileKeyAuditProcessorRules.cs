using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using UnusableFileKeyAudit;
using Xunit;

namespace UnusableFileKeyAudit.Tests.BusinessRules;

public class UnusableFileKeyAuditProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public UnusableFileKeyAuditProcessorRules()
    {
        StoreEmpty();
    }

    private void StoreEmpty()
    {
        _mocker.GetMock<IPodcastRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<Podcast>());
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<TvShow>());
        _mocker.GetMock<INewsOrganisationRepository>().Setup(r => r.GetAll())
            .Returns(AsyncEnumerable.Empty<NewsOrganisation>());
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<Film>());
    }

    [Fact(DisplayName =
        "Unusable file-key audit: a podcast with a usable key and one with an empty key are counted separately, " +
        "and the audit never saves, because the count must be safe against the live catalogue.")]
    public async Task podcast_usable_and_empty_are_counted_and_nothing_is_written()
    {
        // Arrange
        var usable = _fixture.CreatePodcast();
        usable.FileKey = FileKeyFactory.GetFileKey(FileKeyKind.Podcast, usable.Name, usable.Id);
        var empty = _fixture.CreatePodcast();
        empty.FileKey = string.Empty;
        _mocker.GetMock<IPodcastRepository>().Setup(r => r.GetAll())
            .Returns(new[] { usable, empty }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<UnusableFileKeyAuditProcessor>();

        // Act
        var result = await sut.Run(new UnusableFileKeyAuditRequest { Kind = "podcast" });

        // Assert
        result.ByKind[FileKeyKind.Podcast].Scanned.Should().Be(2);
        result.ByKind[FileKeyKind.Podcast].Usable.Should().Be(1);
        result.ByKind[FileKeyKind.Podcast].Unusable.Should().Be(1);
        result.TotalUnusable.Should().Be(1);
        result.ExitCode.Should().Be(0);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
    }

    [Fact(DisplayName =
        "Unusable file-key audit: a news organisation whose key is a bare news- prefix is unusable, because " +
        "IsUsableFileKey rejects placeholder slugs after the kind prefix.")]
    public async Task bare_news_prefix_is_unusable()
    {
        // Arrange
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            FileKey = "news-"
        };
        _mocker.GetMock<INewsOrganisationRepository>().Setup(r => r.GetAll())
            .Returns(new[] { organisation }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<UnusableFileKeyAuditProcessor>();

        // Act
        var result = await sut.Run(new UnusableFileKeyAuditRequest { Kind = "news", List = true });

        // Assert
        result.ByKind[FileKeyKind.NewsOrganisation].Unusable.Should().Be(1);
        result.ByKind[FileKeyKind.NewsOrganisation].Samples.Should().ContainSingle(s => s.Contains("news-"));
    }

    [Fact(DisplayName =
        "Unusable file-key audit: --kind all scans podcasts, TV shows, news organisations and films, because the " +
        "placeholder inventory must cover every public-database parent kind.")]
    public async Task kind_all_scans_four_parent_kinds()
    {
        // Arrange
        var sut = _mocker.CreateInstance<UnusableFileKeyAuditProcessor>();

        // Act
        var result = await sut.Run(new UnusableFileKeyAuditRequest { Kind = "all" });

        // Assert
        result.ByKind.Keys.Should().BeEquivalentTo(
        [
            FileKeyKind.Podcast, FileKeyKind.TvShow, FileKeyKind.NewsOrganisation, FileKeyKind.Film
        ]);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(It.IsAny<TvShow>()), Times.Never);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Verify(r => r.Save(It.IsAny<NewsOrganisation>()), Times.Never);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.Save(It.IsAny<Film>()), Times.Never);
    }

    [Fact(DisplayName =
        "Unusable file-key audit: an unknown --kind fails before any container is read, because a typo must not " +
        "look like a clean empty inventory.")]
    public async Task unknown_kind_throws_before_reads()
    {
        // Arrange
        var sut = _mocker.CreateInstance<UnusableFileKeyAuditProcessor>();

        // Act
        var act = () => sut.Run(new UnusableFileKeyAuditRequest { Kind = "episode" });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.GetAll(), Times.Never);
    }
}
