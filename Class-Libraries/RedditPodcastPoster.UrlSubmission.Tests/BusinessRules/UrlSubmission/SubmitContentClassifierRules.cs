using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class SubmitContentClassifierRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A Netflix catalogue /title/ URL is rejected and is not stored as a TvShow stub, " +
        "because submit needs an episode or watch URL.")]
    public void netflix_title_hub_is_rejected()
    {
        // Arrange
        var signals = Signals(NetflixTitleUrl(), madeAsFilm: true, series: true);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.Reject.Should().BeTrue();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Netflix /watch/ URL for a series episode is a TvShow episode with a parent, " +
        "because a watch URL is not the catalogue hub.")]
    public void netflix_watch_series_is_a_tv_show_episode()
    {
        // Arrange
        var signals = Signals(NetflixWatchUrl(), series: true, madeAsFilm: true);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.HasParent.Should().BeTrue();
        result.Reject.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A standalone made-as-film signal is Film with no parent, " +
        "because cinema versus television premiere does not matter.")]
    public void standalone_made_as_film_is_a_film()
    {
        // Arrange
        var signals = Signals(NetflixWatchUrl(), madeAsFilm: true, series: false);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.HasParent.Should().BeFalse();
        result.Reject.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A series signal wins over a provider movie signal, " +
        "because a miniseries, anthology, or finite series is a TvShow episode and never a Film.")]
    public void series_beats_provider_movie_signal()
    {
        // Arrange
        var signals = Signals(Https($"watch/{_fixture.CreateGuid():N}"), madeAsFilm: true, series: true);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.HasParent.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A Spotify, Apple, or YouTube episode is catalogue Episode with a parent.")]
    public void podcast_service_episode_stays_an_episode()
    {
        // Arrange
        var signals = Signals(Https($"episode/{_fixture.CreateGuid():N}"), podcastServiceEpisode: true);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.Should().BeEquivalentTo(SubmitClassification.PodcastEpisode());
    }

    [Fact(DisplayName =
        "A YouTube news-station signal is a NewsReport with a parent, " +
        "and it is not stored as a podcast episode.")]
    public void youtube_news_station_is_a_news_report()
    {
        // Arrange
        var signals = Signals(
            Https($"watch?v={_fixture.CreateYouTubeId()}"),
            podcastServiceEpisode: true,
            youTubeNewsStation: true);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.HasParent.Should().BeTrue();
        result.Reject.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Vimeo URL without curator confirmation is not persisted as a kind, " +
        "because the classifier may only suggest when Film versus series is ambiguous.")]
    public void vimeo_without_curator_confirmation_requires_a_curator()
    {
        // Arrange
        var signals = Signals(new Uri($"https://vimeo.com/{_fixture.CreateGuid():N}"));

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.RequiresCurator.Should().BeTrue();
        result.Reject.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A Vimeo URL the curator confirmed as a film is Film with no parent.")]
    public void curator_confirmed_vimeo_film_is_a_film()
    {
        // Arrange
        var signals = Signals(
            new Uri($"https://vimeo.com/{_fixture.CreateGuid():N}"),
            madeAsFilm: true,
            curatorConfirmed: true);

        // Act
        var result = SubmitContentClassifier.Classify(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.HasParent.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the submit content-type flag is off, classification falls back to a podcast episode, " +
        "including a rejected Netflix hub and a film, because persist stays Podcast + Episode.")]
    public void flag_off_forces_podcast_episode()
    {
        // Arrange
        var options = new SubmitContentTypesOptions();
        var rejected = SubmitContentClassifier.Classify(Signals(NetflixTitleUrl()));
        var film = SubmitContentClassifier.Classify(Signals(NetflixWatchUrl(), madeAsFilm: true));

        // Act
        var rejectedWhenOff = SubmitContentClassifier.WhenEnabled(rejected, enabled: false);
        var filmWhenOff = SubmitContentClassifier.WhenEnabled(film, enabled: false);
        var filmWhenOn = SubmitContentClassifier.WhenEnabled(film, enabled: true);

        // Assert
        options.Enabled.Should().BeFalse();
        rejectedWhenOff.Should().BeEquivalentTo(SubmitClassification.PodcastEpisode());
        filmWhenOff.Should().BeEquivalentTo(SubmitClassification.PodcastEpisode());
        filmWhenOn.ContentKind.Should().Be(SubmitClassification.Film);
    }

    [Fact(DisplayName =
        "A resolved streaming item marked made-as-film becomes a Film signal, " +
        "and a resolved item with a show name becomes a series signal.")]
    public void resolved_item_supplies_film_and_series_signals()
    {
        // Arrange
        var filmUrl = NetflixWatchUrl();
        var film = new CategorisedItem(
            null,
            null,
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                StreamingService.Netflix,
                Url: filmUrl,
                Title: _fixture.CreateTitle(),
                MadeAsFilm: true),
            Service.Other);
        var seriesUrl = NetflixWatchUrl();
        var seriesName = _fixture.CreateTitle();
        var series = new CategorisedItem(
            null,
            null,
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                StreamingService.Netflix,
                Url: seriesUrl,
                Title: _fixture.CreateTitle(),
                ShowName: seriesName),
            Service.Other);

        // Act
        var filmSignals = SubmitContentClassifier.FromSubmission(filmUrl, film);
        var seriesSignals = SubmitContentClassifier.FromSubmission(seriesUrl, series);

        // Assert
        filmSignals.MadeAsFilm.Should().BeTrue();
        filmSignals.Series.Should().BeFalse();
        SubmitContentClassifier.Classify(filmSignals).ContentKind.Should().Be(SubmitClassification.Film);
        seriesSignals.Series.Should().BeTrue();
        seriesSignals.MadeAsFilm.Should().BeFalse();
        SubmitContentClassifier.Classify(seriesSignals).ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
    }

    [Fact(DisplayName =
        "A BBC /news/ URL is a NewsReport with a parent, and an iPlayer episode URL is not, " +
        "because news is a separate matcher from Sounds and iPlayer.")]
    public void bbc_news_url_is_a_news_report()
    {
        // Arrange
        var news = new SubmitClassificationSignals(
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"));
        var iplayer = new SubmitClassificationSignals(
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateGuid():N}"));

        // Act
        var newsResult = SubmitContentClassifier.Classify(news);
        var iplayerResult = SubmitContentClassifier.Classify(iplayer);

        // Assert
        newsResult.ContentKind.Should().Be(SubmitClassification.NewsReport);
        newsResult.HasParent.Should().BeTrue();
        newsResult.Reject.Should().BeFalse();
        iplayerResult.ContentKind.Should().Be(SubmitClassification.Episode);
    }

    private SubmitClassificationSignals Signals(
        Uri url,
        bool podcastServiceEpisode = false,
        bool madeAsFilm = false,
        bool series = false,
        bool news = false,
        bool youTubeNewsStation = false,
        bool curatorConfirmed = false) =>
        new(url, podcastServiceEpisode, madeAsFilm, series, news, youTubeNewsStation, curatorConfirmed);

    private Uri NetflixTitleUrl() =>
        new($"https://www.netflix.com/title/{_fixture.CreateGuid():N}");

    private Uri NetflixWatchUrl() =>
        new($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}");

    private static Uri Https(string path) => new($"https://example.com/{path}");
}
