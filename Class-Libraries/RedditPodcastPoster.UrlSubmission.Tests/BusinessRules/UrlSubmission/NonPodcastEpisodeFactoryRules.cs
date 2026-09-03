using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Factories;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class NonPodcastEpisodeFactoryRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Creating an episode from a BBC Sounds URL stores title, description, release, duration, and the Sounds catalog URL plus image.")]
    public void sounds_item_writes_bbc_sounds_catalog_url_and_core_fields()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var duration = _fixture.CreateDuration();
        var image = _fixture.Create<Uri>();
        var categorised = CreateNonPodcastItem(
            NonPodcastService.BBC,
            url,
            title,
            description,
            release,
            duration,
            image);
        var sut = CreateSut(TimeSpan.Zero);

        // Act
        var episode = sut.CreateEpisode(categorised);

        // Assert
        episode.Title.Should().Be(title);
        episode.Description.Should().Be(description);
        episode.Release.Should().Be(release);
        episode.Length.Should().Be(duration);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcSounds).Should().Be(url);
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.BbcSounds).Should().Be(image);
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.BbcIplayer).Should().BeFalse();
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.InternetArchive).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Creating an episode from a BBC iPlayer URL stores the iPlayer catalog key, not Sounds, " +
        "because iPlayer and Sounds are different destinations.")]
    public void iplayer_item_writes_bbc_iplayer_catalog_url()
    {
        // Arrange
        var url = BbcIplayerUrl();
        var categorised = CreateNonPodcastItem(
            NonPodcastService.BBC,
            url,
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.CreateDuration(),
            _fixture.Create<Uri>());
        var sut = CreateSut(TimeSpan.Zero);

        // Act
        var episode = sut.CreateEpisode(categorised);

        // Assert
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.BbcIplayer).Should().Be(url);
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.BbcSounds).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Creating an episode from an Internet Archive URL stores the archive catalog URL and image.")]
    public void archive_item_writes_internet_archive_catalog_url_and_image()
    {
        // Arrange
        var url = InternetArchiveUrl();
        var image = _fixture.Create<Uri>();
        var categorised = CreateNonPodcastItem(
            NonPodcastService.InternetArchive,
            url,
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.CreateDuration(),
            image);
        var sut = CreateSut(TimeSpan.Zero);

        // Act
        var episode = sut.CreateEpisode(categorised);

        // Assert
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.InternetArchive).Should().Be(url);
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.InternetArchive).Should().Be(image);
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.BbcSounds).Should().BeFalse();
        EpisodeServicePresence.HasUrl(episode, ServiceKeys.BbcIplayer).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Creating an episode from a Vimeo URL stores the vimeo catalog URL and image, " +
        "the same services map used for Sounds and Internet Archive.")]
    public void vimeo_item_writes_vimeo_catalog_url_and_image()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        var image = _fixture.Create<Uri>();
        var categorised = CreateNonPodcastItem(
            NonPodcastService.Vimeo,
            url,
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.CreateDuration(),
            image);
        var sut = CreateSut(TimeSpan.Zero);

        // Act
        var episode = sut.CreateEpisode(categorised);

        // Assert
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Vimeo).Should().Be(url);
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Vimeo).Should().Be(image);
    }

    [Fact(DisplayName =
        "When a Sounds episode is shorter than the posting minimum duration, the created episode is ignored.")]
    public void shorter_than_minimum_duration_is_ignored()
    {
        // Arrange
        var duration = _fixture.CreateDuration();
        var minimum = duration + TimeSpan.FromSeconds(1);
        var categorised = CreateNonPodcastItem(
            NonPodcastService.BBC,
            BbcSoundsUrl(),
            _fixture.CreateTitle(),
            _fixture.Create<string>(),
            DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay()),
            duration,
            null);
        var sut = CreateSut(minimum);

        // Act
        var episode = sut.CreateEpisode(categorised);

        // Assert
        episode.Ignored.Should().BeTrue();
        episode.Length.Should().Be(duration);
    }

    [Fact(DisplayName =
        "A BBC Sounds resolved item exposes BBCUrl and not InternetArchiveUrl, " +
        "so factory and enricher write the BBC catalog slot.")]
    public void bbc_resolved_item_exposes_bbc_url_helper()
    {
        // Arrange
        var url = BbcSoundsUrl();

        // Act
        var item = new ResolvedNonPodcastServiceItem(NonPodcastService.BBC, Url: url);

        // Assert
        item.BBCUrl.Should().Be(url);
        item.InternetArchiveUrl.Should().BeNull();
    }

    [Fact(DisplayName =
        "An Internet Archive resolved item exposes InternetArchiveUrl and not BBCUrl.")]
    public void archive_resolved_item_exposes_internet_archive_url_helper()
    {
        // Arrange
        var url = InternetArchiveUrl();

        // Act
        var item = new ResolvedNonPodcastServiceItem(NonPodcastService.InternetArchive, Url: url);

        // Assert
        item.InternetArchiveUrl.Should().Be(url);
        item.BBCUrl.Should().BeNull();
    }

    private EpisodeFactory CreateSut(TimeSpan minimumDuration)
    {
        var mocker = new AutoMocker();
        mocker.GetMock<IDescriptionHelper>()
            .Setup(x => x.EnrichMissingDescription(It.IsAny<CategorisedItem>()))
            .Returns(_fixture.Create<string>());
        mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = minimumDuration,
            TweetDays = 7,
            RedditDays = 7,
            BlueSkyDays = 7,
            CategoriserDays = 7
        }));
        return mocker.CreateInstance<EpisodeFactory>();
    }

    private CategorisedItem CreateNonPodcastItem(
        NonPodcastService service,
        Uri url,
        string title,
        string description,
        DateTime release,
        TimeSpan duration,
        Uri? image) =>
        new(
            null,
            [],
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                service,
                Url: url,
                Title: title,
                Description: description,
                Image: image,
                Release: release,
                Duration: duration),
            Service.Other);

    private Uri BbcSoundsUrl() =>
        new($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

    private Uri BbcIplayerUrl() =>
        new($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");

    private Uri InternetArchiveUrl() =>
        new($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
}
