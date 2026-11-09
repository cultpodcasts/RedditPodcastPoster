using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueMigrateIdentifyRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A BBC news URL marks the row as a NewsReport candidate that needs a curator --podcast-id for apply, " +
        "because News migrate is first and S-008 forbids unattended news-station moves.")]
    public void bbc_news_url_is_a_news_candidate_that_needs_a_podcast_id()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"))
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresPodcastId.Should().BeTrue();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Mixed News and Film signals stay Episode and need a curator, " +
        "because a News station candidate requires every classified URL to be News.")]
    public void mixed_news_and_film_signals_are_not_a_news_station()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true),
            new SubmitClassificationSignals(new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"))
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Mixed series and made-as-film URLs stay Episode and need a curator, " +
        "because a migrate candidate requires every classified URL to be the same kind.")]
    public void mixed_series_and_film_signals_are_not_a_tv_station()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                Series: true),
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A standalone made-as-film signal with no News or series is a Film candidate.")]
    public void film_only_is_a_film_candidate()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                MadeAsFilm: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A series signal with no News or Film is a TvShowEpisode candidate, " +
        "and it does not need a curator --podcast-id.")]
    public void series_only_is_a_tv_candidate()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://www.netflix.com/watch/{_fixture.CreateGuid():N}"),
                Series: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Spotify, Apple, or YouTube entertainment URLs stay Episode, " +
        "because those rows are not corpus-migrate candidates.")]
    public void podcast_service_urls_stay_episode()
    {
        // Arrange
        var signals = new[]
        {
            new SubmitClassificationSignals(
                new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
                PodcastServiceEpisode: true)
        };

        // Act
        var result = CatalogueMigrateIdentify.FromSignals(signals);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A stored episode whose service URL is a BBC news page is a NewsReport candidate that needs a curator --podcast-id, " +
        "because corpus identify reads episode URLs without scraping.")]
    public void stored_bbc_news_episode_url_is_a_news_candidate()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresPodcastId.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A stored Spotify episode stays Episode, " +
        "because podcast-service URLs are not corpus-migrate candidates.")]
    public void stored_spotify_episode_stays_episode()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A podcast with no episode service URLs stays Episode, " +
        "because identify has no signals to classify.")]
    public void episodes_with_no_urls_stay_episode()
    {
        // Arrange
        var episode = _fixture.CreateEpisode();

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A stored Spotify URL plus a BBC news URL on the same podcast stays Episode and needs a curator, " +
        "because mixed entertainment and News is not a News station candidate.")]
    public void stored_spotify_plus_bbc_news_is_not_a_news_station()
    {
        // Arrange
        var newsEpisode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        var spotifyEpisode = _fixture.CreateEpisode();
        EpisodeServicePresence.Upsert(
            spotifyEpisode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromEpisodes([newsEpisode, spotifyEpisode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeTrue();
        CatalogueMigrateIdentify.IsNewsReportEpisode(newsEpisode).Should().BeTrue();
        CatalogueMigrateIdentify.IsNewsReportEpisode(spotifyEpisode).Should().BeFalse();
        CatalogueMigrateIdentify.NonNewsEpisodeIds([newsEpisode, spotifyEpisode])
            .Should()
            .Equal(spotifyEpisode.Id);
    }

    [Fact(DisplayName =
        "A YouTube-only publisher whose name is four ASCII letters is a NewsReport candidate that needs a curator --podcast-id, " +
        "because S-008 dry-run may use call-sign style names without scraping.")]
    public void youtube_only_four_letter_name_is_a_news_station_candidate()
    {
        // Arrange
        var callSign = CreateFourLetterName();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = callSign;
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresPodcastId.Should().BeTrue();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A YouTube-only publisher whose name contains the word News is a NewsReport candidate that needs a curator --podcast-id, " +
        "because many US stations brand as Eyewitness News or 9 News rather than a four-letter call sign.")]
    public void youtube_only_name_with_news_word_is_a_news_organisation_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = $"{_fixture.CreateTitle()} News";
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresPodcastId.Should().BeTrue();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A YouTube-only publisher whose name is a US network plus a channel number is a NewsReport candidate that needs a curator --podcast-id, " +
        "because affiliates often publish as ABC7 or FOX 5 instead of four call letters.")]
    public void youtube_only_us_affiliate_channel_number_is_a_news_organisation_candidate()
    {
        // Arrange
        var channelNumber = Math.Abs(_fixture.Create<int>()) % 9 + 1;
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = $"ABC{channelNumber}";
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresPodcastId.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A YouTube-only publisher whose name includes 24/7 is a NewsReport candidate that needs a curator --podcast-id, " +
        "because rolling-news channels outside the US brand that way rather than with a News word.")]
    public void youtube_only_round_the_clock_name_is_a_news_organisation_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = $"{_fixture.CreateTitle()} 24/7";
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresPodcastId.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A YouTube-only publisher whose name has no News word, 24/7 mark, or US affiliate channel number stays Episode, " +
        "because entertainment YouTube channels must not become NewsOrganisation from the name heuristic.")]
    public void youtube_only_entertainment_name_is_not_a_news_organisation()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = _fixture.CreateTitle();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A publisher whose name contains News but also has a Spotify id stays Episode, " +
        "because the news-organisation heuristic is YouTube-only.")]
    public void news_word_name_with_spotify_is_not_a_news_organisation()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = $"{_fixture.CreateTitle()} News";
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = _fixture.CreateSpotifyId();
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A YouTube-only publisher with no iPlayer series and a name that does not look like news stays Episode, " +
        "because there is no curator TV-programme name list: TV comes from stored iPlayer series or --podcast-id.")]
    public void youtube_only_publisher_is_not_tv_by_name()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = _fixture.CreateTitle();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A YouTube-only publisher whose name contains News and the word Podcast stays Episode, " +
        "because a show that calls itself a podcast is still a podcast catalogue row.")]
    public void youtube_only_news_name_that_says_podcast_stays_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = $"{_fixture.CreateTitle()} News Podcast";
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A four-letter YouTube publisher that also has a Spotify id stays Episode, " +
        "because the news-station heuristic is YouTube-only.")]
    public void four_letter_name_with_spotify_is_not_a_news_station()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = CreateFourLetterName();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = _fixture.CreateSpotifyId();
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Two stored BBC iPlayer episode URLs on a publisher with no Spotify or Apple ids are a TvShowEpisode candidate, " +
        "because a series has more than one playable and iPlayer episode paths are stored without scrape flags.")]
    public void two_iplayer_episode_urls_are_a_tv_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [first, second]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.RequiresPodcastId.Should().BeFalse();
        result.RequiresCurator.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A single stored BBC iPlayer episode URL is not a TV candidate, " +
        "because Film is a one-off and identify must not treat one playable as a series.")]
    public void one_iplayer_episode_url_is_not_a_tv_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
    }

    [Fact(DisplayName =
        "Two stored BBC iPlayer episode URLs plus one episode without iPlayer are still a TvShowEpisode candidate, " +
        "because series identify counts iPlayer playables rather than requiring every episode to have iPlayer.")]
    public void two_iplayer_urls_plus_one_without_iplayer_are_a_tv_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        var withoutIplayer = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [first, second, withoutIplayer]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Two stored BBC iPlayer episode URLs on a publisher that also has a Spotify id stay Episode, " +
        "because publisher Spotify means the show is still a podcast catalogue row.")]
    public void two_iplayer_urls_with_publisher_spotify_stay_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [first, second]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Two stored BBC iPlayer episode URLs on a publisher that also has an Apple id stay Episode, " +
        "because publisher Apple means the show is still a podcast catalogue row.")]
    public void two_iplayer_urls_with_publisher_apple_stay_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = _fixture.CreateAppleId();
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [first, second]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A four-letter YouTube publisher with two stored iPlayer episode URLs is a TvShowEpisode candidate, " +
        "because a stored iPlayer series is TV and is not YouTube-only news.")]
    public void four_letter_youtube_publisher_with_two_iplayer_urls_is_tv()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = CreateFourLetterName();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(podcast);
        var second = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            first,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);
        EpisodeServicePresence.Upsert(
            second,
            "bbcIplayer",
            new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
            image: null);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [first, second]);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A four-letter YouTube publisher with no active episodes stays Episode, " +
        "because the news-station heuristic needs YouTube-only playables and must not flag an empty publisher.")]
    public void four_letter_youtube_publisher_with_no_episodes_stays_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = CreateFourLetterName();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, []);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.RequiresPodcastId.Should().BeFalse();
    }

    private string CreateFourLetterName() =>
        string.Create(4, _fixture.Create<int>(), static (span, value) =>
        {
            var n = value;
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[Math.Abs(n % 26)];
                n = HashCode.Combine(n, i);
            }
        });
}
