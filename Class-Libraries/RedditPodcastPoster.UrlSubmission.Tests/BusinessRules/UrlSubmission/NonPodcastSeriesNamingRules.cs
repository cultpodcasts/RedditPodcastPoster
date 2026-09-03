using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class NonPodcastSeriesNamingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private string? _createdShowName;

    public NonPodcastSeriesNamingRules()
    {
        _mocker.GetMock<IPodcastFactory>()
            .Setup(x => x.Create(It.IsAny<string>()))
            .ReturnsAsync((string name) =>
            {
                _createdShowName = name;
                return _fixture.CreatePodcast(p => p.Name = name);
            });
        _mocker.GetMock<IEpisodeFactory>()
            .Setup(x => x.CreateEpisode(It.IsAny<CategorisedItem>()))
            .Returns(() => _fixture.CreateStoredEpisode(_fixture.CreatePodcast()));
        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));
        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));
    }

    [Fact(DisplayName =
        "When a submit supplies an explicit series name, the new podcast uses that name, " +
        "not the episode title or extracted brand.")]
    public async Task explicit_series_name_wins_over_extracted_brand()
    {
        // Arrange
        var explicitName = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var brand = _fixture.CreateTitle();
        var categorised = CreateItem(NonPodcastService.BBC, episodeTitle, _fixture.Create<string>(), brand);
        var sut = _mocker.CreateInstance<PodcastAndEpisodeFactory>();

        // Act
        var response = await sut.CreatePodcastWithEpisode(categorised, explicitName);

        // Assert
        response.NewPodcast.Name.Should().Be(explicitName);
        _createdShowName.Should().Be(explicitName);
        _createdShowName.Should().NotBe(episodeTitle);
        _createdShowName.Should().NotBe(brand);
    }

    [Fact(DisplayName =
        "When creating a BBC series without an explicit name, the new podcast is named after the programme/brand " +
        "when Sounds or iPlayer metadata has a series field distinct from the episode title.")]
    public async Task bbc_uses_series_when_present()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var seriesName = _fixture.CreateTitle();
        var categorised = CreateItem(NonPodcastService.BBC, episodeTitle, "BBC", seriesName);
        var sut = _mocker.CreateInstance<PodcastAndEpisodeFactory>();

        // Act
        var response = await sut.CreatePodcastWithEpisode(categorised);

        // Assert
        response.NewPodcast.Name.Should().Be(seriesName);
        _createdShowName.Should().Be(seriesName);
        _createdShowName.Should().NotBe(episodeTitle);
    }

    [Fact(DisplayName =
        "When BBC metadata has no series/brand field, the new podcast falls back to the episode title.")]
    public async Task bbc_without_series_falls_back_to_episode_title()
    {
        // Arrange
        var episodeTitle = _fixture.CreateTitle();
        var categorised = CreateItem(NonPodcastService.BBC, episodeTitle, "BBC", showName: null);
        var sut = _mocker.CreateInstance<PodcastAndEpisodeFactory>();

        // Act
        var response = await sut.CreatePodcastWithEpisode(categorised);

        // Assert
        response.NewPodcast.Name.Should().Be(episodeTitle);
    }

    [Fact(DisplayName =
        "Internet Archive items have no series, so the new podcast may be named after the item title.")]
    public async Task archive_without_series_uses_item_title()
    {
        // Arrange
        var itemTitle = _fixture.CreateTitle();
        var uploader = _fixture.Create<string>();
        var categorised = CreateItem(NonPodcastService.InternetArchive, itemTitle, uploader, showName: null);
        var sut = _mocker.CreateInstance<PodcastAndEpisodeFactory>();

        // Act
        var response = await sut.CreatePodcastWithEpisode(categorised);

        // Assert
        response.NewPodcast.Name.Should().Be(itemTitle);
        response.NewPodcast.Publisher.Should().Be(uploader);
        _createdShowName.Should().NotBe(uploader);
    }

    [Fact(DisplayName =
        "When a Vimeo submit has no explicit series name, the new podcast is named after the author/user, " +
        "not the video title, because Vimeo oEmbed has no BBC-style series field.")]
    public async Task vimeo_without_explicit_name_uses_author()
    {
        // Arrange
        var videoTitle = _fixture.CreateTitle();
        var author = _fixture.Create<string>();
        var categorised = CreateItem(NonPodcastService.Vimeo, videoTitle, author, showName: null);
        var sut = _mocker.CreateInstance<PodcastAndEpisodeFactory>();

        // Act
        var response = await sut.CreatePodcastWithEpisode(categorised);

        // Assert
        response.NewPodcast.Name.Should().Be(author);
        response.NewPodcast.Publisher.Should().Be(author);
        _createdShowName.Should().NotBe(videoTitle);
    }

    private CategorisedItem CreateItem(
        NonPodcastService service,
        string title,
        string publisher,
        string? showName)
    {
        var host = service switch
        {
            NonPodcastService.BBC => $"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}",
            NonPodcastService.InternetArchive => $"https://archive.org/details/{_fixture.CreateYouTubeId()}",
            _ => $"https://vimeo.com/{_fixture.CreateAppleId()}"
        };
        return new CategorisedItem(
            null,
            [],
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                service,
                Url: new Uri(host),
                Title: title,
                Publisher: publisher,
                ShowName: showName),
            Service.Other);
    }
}
