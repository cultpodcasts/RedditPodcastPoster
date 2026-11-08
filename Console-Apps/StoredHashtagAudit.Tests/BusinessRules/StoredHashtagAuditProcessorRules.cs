using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using StoredHashtagAudit;
using Xunit;

namespace StoredHashtagAudit.Tests.BusinessRules;

public class StoredHashtagAuditProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public StoredHashtagAuditProcessorRules()
    {
        StoreEmpty();
    }

    private void StoreEmpty()
    {
        _mocker.GetMock<ISubjectRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<Subject>());
        _mocker.GetMock<IPodcastRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<Podcast>());
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<TvShow>());
        _mocker.GetMock<INewsOrganisationRepository>().Setup(r => r.GetAll())
            .Returns(AsyncEnumerable.Empty<NewsOrganisation>());
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<Film>());
        _mocker.GetMock<IEpisodeRepository>().Setup(r => r.GetAll()).Returns(AsyncEnumerable.Empty<Episode>());
        _mocker.GetMock<ITvShowEpisodeRepository>().Setup(r => r.GetAll())
            .Returns(AsyncEnumerable.Empty<TvShowEpisode>());
        _mocker.GetMock<INewsReportRepository>().Setup(r => r.GetAll())
            .Returns(AsyncEnumerable.Empty<NewsReport>());
    }

    [Fact(DisplayName =
        "Stored hashtag audit: a podcast whose HashTag is reproduced as-is and one with a hyphen are counted " +
        "separately, and the audit never saves, because the inventory must be safe against the live catalogue.")]
    public async Task podcast_reproducible_and_hyphenated_are_counted_and_nothing_is_written()
    {
        // Arrange
        var clean = _fixture.CreatePodcast();
        clean.HashTag = "#CleanTag";
        var hyphenated = _fixture.CreatePodcast();
        hyphenated.HashTag = "#Some-Tag";
        _mocker.GetMock<IPodcastRepository>().Setup(r => r.GetAll())
            .Returns(new[] { clean, hyphenated }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var result = await sut.Run(new StoredHashtagAuditRequest { Kind = "podcast" });

        // Assert
        result.ByKind[HashtagEntityKind.Podcast].Scanned.Should().Be(2);
        result.ByKind[HashtagEntityKind.Podcast].WithHashTag.Should().Be(2);
        result.ByKind[HashtagEntityKind.Podcast].Reproducible.Should().Be(1);
        result.ByKind[HashtagEntityKind.Podcast].NonReproducible.Should().Be(1);
        result.TotalNonReproducible.Should().Be(1);
        result.ExitCode.Should().Be(0);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
    }

    [Fact(DisplayName =
        "Stored hashtag audit: a subject HashTag with an ampersand is non-reproducible and listed with its " +
        "ToTagText, because posting would omit that tag rather than write a shortened form.")]
    public async Task subject_ampersand_tag_is_listed_as_non_reproducible()
    {
        // Arrange
        var subject = new Subject(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            HashTag = "#Q&A"
        };
        _mocker.GetMock<ISubjectRepository>().Setup(r => r.GetAll())
            .Returns(new[] { subject }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var result = await sut.Run(new StoredHashtagAuditRequest { Kind = "subject", List = true });

        // Assert
        result.ByKind[HashtagEntityKind.Subject].NonReproducible.Should().Be(1);
        result.ByKind[HashtagEntityKind.Subject].Samples.Should().ContainSingle(s =>
            s.Contains("token=\"#Q&A\"", StringComparison.Ordinal) &&
            s.Contains("toTagText=QA", StringComparison.Ordinal));
        _mocker.GetMock<ISubjectRepository>().Verify(r => r.Save(It.IsAny<Subject>()), Times.Never);
    }

    [Fact(DisplayName =
        "Stored hashtag audit: an episode with a blank HashTag is blank rather than non-reproducible, because " +
        "empty tags are not posting risk.")]
    public async Task blank_episode_hashtag_is_blank_not_non_reproducible()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        episode.HashTag = "   ";
        _mocker.GetMock<IEpisodeRepository>().Setup(r => r.GetAll())
            .Returns(new[] { episode }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var result = await sut.Run(new StoredHashtagAuditRequest { Kind = "episode" });

        // Assert
        result.ByKind[HashtagEntityKind.Episode].Blank.Should().Be(1);
        result.ByKind[HashtagEntityKind.Episode].NonReproducible.Should().Be(0);
        result.ByKind[HashtagEntityKind.Episode].WithHashTag.Should().Be(0);
    }

    [Fact(DisplayName =
        "Stored hashtag audit: a space-delimited multi-tag episode HashTag where every token is reproducible " +
        "counts as one reproducible row, because posting splits then checks each token.")]
    public async Task multi_tag_all_clean_is_reproducible()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        episode.HashTag = "#AlphaBeta #CleanTag";
        _mocker.GetMock<IEpisodeRepository>().Setup(r => r.GetAll())
            .Returns(new[] { episode }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var result = await sut.Run(new StoredHashtagAuditRequest { Kind = "episode" });

        // Assert
        result.ByKind[HashtagEntityKind.Episode].WithHashTag.Should().Be(1);
        result.ByKind[HashtagEntityKind.Episode].Reproducible.Should().Be(1);
        result.ByKind[HashtagEntityKind.Episode].NonReproducible.Should().Be(0);
    }

    [Fact(DisplayName =
        "Stored hashtag audit: a space-delimited multi-tag episode HashTag with one hyphenated token is " +
        "non-reproducible and samples name that token, because any failing token fails the row.")]
    public async Task multi_tag_one_hyphen_is_non_reproducible_and_samples_name_offender()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        episode.HashTag = "#CleanTag #Some-Tag";
        _mocker.GetMock<IEpisodeRepository>().Setup(r => r.GetAll())
            .Returns(new[] { episode }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var result = await sut.Run(new StoredHashtagAuditRequest { Kind = "episode", List = true });

        // Assert
        result.ByKind[HashtagEntityKind.Episode].Reproducible.Should().Be(0);
        result.ByKind[HashtagEntityKind.Episode].NonReproducible.Should().Be(1);
        result.ByKind[HashtagEntityKind.Episode].Samples.Should().ContainSingle(s =>
            s.Contains("token=\"#Some-Tag\"", StringComparison.Ordinal) &&
            s.Contains("toTagText=SomeTag", StringComparison.Ordinal) &&
            !s.Contains("token=\"#CleanTag\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName =
        "Stored hashtag audit: --kind all scans subjects, publishers and playables, because the inventory must " +
        "cover every catalogue HashTag field.")]
    public async Task kind_all_scans_every_hashtag_entity_kind()
    {
        // Arrange
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var result = await sut.Run(new StoredHashtagAuditRequest { Kind = "all" });

        // Assert
        result.ByKind.Keys.Should().BeEquivalentTo(
        [
            HashtagEntityKind.Subject,
            HashtagEntityKind.Podcast,
            HashtagEntityKind.TvShow,
            HashtagEntityKind.NewsOrganisation,
            HashtagEntityKind.Film,
            HashtagEntityKind.Episode,
            HashtagEntityKind.TvShowEpisode,
            HashtagEntityKind.NewsReport
        ]);
        _mocker.GetMock<ISubjectRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<IFilmRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<INewsReportRepository>().Verify(r => r.GetAll(), Times.Once);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(r => r.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "Stored hashtag audit: an unknown --kind fails before any container is read, because a typo must not " +
        "look like a clean empty inventory.")]
    public async Task unknown_kind_throws_before_reads()
    {
        // Arrange
        var sut = _mocker.CreateInstance<StoredHashtagAuditProcessor>();

        // Act
        var act = () => sut.Run(new StoredHashtagAuditRequest { Kind = "person" });

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _mocker.GetMock<ISubjectRepository>().Verify(r => r.GetAll(), Times.Never);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.GetAll(), Times.Never);
    }
}
