using System.Globalization;
using CatalogueMigrateIdentify;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace CatalogueMigrateIdentify.Tests.BusinessRules;

public class CatalogueMigrateIdentifyProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly AutoMocker _mocker;

    public CatalogueMigrateIdentifyProcessorRules()
    {
        _mocker = new AutoMocker();
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use(NullLogger<CatalogueMigrateIdentifyProcessor>.Instance);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, CancellationToken _) =>
                new CatalogueAuthorityPreview(
                    query,
                    1,
                    null,
                    null,
                    null,
                    [query],
                    Resolution: CatalogueAuthorityResolution.Unique));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewFilmAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, CancellationToken _) =>
                new CatalogueAuthorityPreview(query, 0, null, null, null, []));
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when --apply is set, then the tool exits 2 without scanning, " +
        "because --apply is always refused and writes are not implemented.")]
    public async Task apply_is_refused_without_scanning()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest { Apply = true });

        // Assert
        result.ExitCode.Should().Be(2);
        result.CandidateCount.Should().Be(0);
        result.Authority.Should().BeEmpty();
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a stored episode URL is a BBC news page, then dry-run counts one candidate " +
        "and does not write, because identify is report-only.")]
    public async Task stored_bbc_news_url_is_a_dry_run_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(1);
        result.Authority.Should().BeEmpty();
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when episode URLs are Spotify only, then dry-run counts no candidates, " +
        "because podcast-service rows stay Episode.")]
    public async Task stored_spotify_is_not_a_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when the podcast is removed, then it is skipped, " +
        "because removed publishers are not migrate candidates.")]
    public async Task removed_podcast_is_skipped()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when the only news URL is on a removed episode, then the podcast is not a candidate, " +
        "because identify ignores removed episodes.")]
    public async Task removed_episode_is_ignored()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Removed = true);
        EpisodeServicePresence.Upsert(
            episode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when --podcast-id is set, then only that podcast is scanned.")]
    public async Task podcast_id_limits_the_scan()
    {
        // Arrange
        var newsPodcast = _fixture.CreatePodcast();
        var otherPodcast = _fixture.CreatePodcast();
        var newsEpisode = _fixture.CreateStoredEpisode(newsPodcast);
        var otherEpisode = _fixture.CreateStoredEpisode(otherPodcast);
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        EpisodeServicePresence.Upsert(
            otherEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        _podcasts.Seed(newsPodcast, otherPodcast);
        _episodes.Seed(newsEpisode, otherEpisode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest { PodcastId = newsPodcast.Id });

        // Assert
        result.CandidateCount.Should().Be(1);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a YouTube-only four-letter publisher has a YouTube episode, " +
        "then dry-run counts one News candidate, because S-008 heuristics apply without BBC news URLs.")]
    public async Task youtube_only_four_letter_is_a_dry_run_news_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = CreateFourLetterName();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(1);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when two stored iPlayer episode URLs exist and the publisher has no Spotify or Apple ids, " +
        "then dry-run counts one TV candidate and checks that show, because signals select the TV and film rows that may be verified.")]
    public async Task two_iplayer_urls_are_a_dry_run_tv_candidate()
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
        _podcasts.Seed(podcast);
        _episodes.Seed(first, second);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(1);
        result.AuthorityLookupSkipped.Should().BeFalse();
        var report = result.Authority.Should().ContainSingle().Subject;
        report.PodcastId.Should().Be(podcast.Id);
        report.ContentKind.Should().Be("TvShowEpisode");
        report.Query.Should().Be(podcast.Name);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a news candidate sits beside one TV candidate, then only the TV candidate is checked, because news is not sent to TMDB or TheTVDB.")]
    public async Task news_candidate_is_not_sent_to_tmdb_or_thetvdb()
    {
        // Arrange
        var news = _fixture.CreatePodcast();
        var newsEpisode = _fixture.CreateStoredEpisode(news);
        EpisodeServicePresence.Upsert(
            newsEpisode,
            "bbcSounds",
            new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}"),
            image: null);
        var show = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        SeedIplayerPair(show);
        _podcasts.Seed(news);
        _episodes.Seed(newsEpisode);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.CandidateCount.Should().Be(2);
        result.AuthorityLookupSkipped.Should().BeFalse();
        result.Authority.Select(report => report.PodcastId).Should().Equal(show.Id);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when signal-identified TV and film candidates exceed the lookup limit, then none are sent to TMDB or TheTVDB, because a corpus-sized set must not be submitted.")]
    public async Task too_many_signal_matches_are_not_sent_to_tmdb_or_thetvdb()
    {
        // Arrange
        var overLimit = CatalogueMigrateIdentifyProcessor.MaxSignalAuthorityLookups + 1;
        for (var i = 0; i < overLimit; i++)
        {
            var podcast = _fixture.CreatePodcast(p =>
            {
                p.SpotifyId = string.Empty;
                p.AppleId = null;
            });
            SeedIplayerPair(podcast);
        }

        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TMDB must not be called for a corpus-sized set."));
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(overLimit);
        result.AuthorityLookupSkipped.Should().BeTrue();
        result.Authority.Should().BeEmpty();
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a publisher matches several programmes, then each episode title is checked and the publisher film is not used, because a channel is not one programme.")]
    public async Task several_programmes_are_checked_as_separate_episode_titles()
    {
        // Arrange
        var show = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var firstTitle = _fixture.CreateTitle();
        var secondTitle = _fixture.CreateTitle();
        var first = _fixture.CreateStoredEpisode(show, episode => episode.Title = firstTitle);
        var second = _fixture.CreateStoredEpisode(show, episode => episode.Title = secondTitle);
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
        _podcasts.Seed(show);
        _episodes.Seed(first, second);
        var filmId = _fixture.CreateAppleId();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                2,
                null,
                null,
                null,
                [firstTitle, secondTitle],
                Resolution: CatalogueAuthorityResolution.SeveralProgrammes));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(
                It.Is<string>(query => query != show.Name),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, CancellationToken _) =>
                new CatalogueAuthorityPreview(
                    query,
                    0,
                    null,
                    null,
                    null,
                    [],
                    Resolution: CatalogueAuthorityResolution.NotInAuthority));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewFilmAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                1,
                ImdbId(),
                (int)(filmId % 1_000_000_000L),
                null,
                [show.Name],
                Resolution: CatalogueAuthorityResolution.Unique));
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.Authority.Select(report => report.Query).Should().BeEquivalentTo(firstTitle, secondTitle);
        result.Authority.Should().OnlyContain(report =>
            report.ContentKind == "TvShowEpisode"
            && report.Resolution == CatalogueAuthorityResolution.NotInAuthority
            && report.ImdbId == null
            && report.TmdbId == null);
        result.Authority.Should().NotContain(report => report.Query == show.Name);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when several episodes on a non-unique publisher share a title brand, then that brand is the programme that is checked, because the channel name is not the show.")]
    public async Task shared_episode_brand_is_the_programme_query()
    {
        // Arrange
        var show = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var brand = _fixture.CreateTitle(2);
        var first = _fixture.CreateStoredEpisode(show, episode => episode.Title = $"{brand} - {_fixture.CreateTitle()}");
        var second = _fixture.CreateStoredEpisode(show, episode => episode.Title = $"{brand} - {_fixture.CreateTitle()} - BBC One");
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
        _podcasts.Seed(show);
        _episodes.Seed(first, second);
        var imdbId = ImdbId();
        var tmdbId = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        if (tmdbId == 0)
        {
            tmdbId = 1;
        }

        var tvdbId = _fixture.CreateAppleId();
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                2,
                null,
                null,
                null,
                [show.Name],
                Resolution: CatalogueAuthorityResolution.SeveralProgrammes));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(brand, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                brand,
                1,
                imdbId,
                tmdbId,
                tvdbId,
                [brand],
                Resolution: CatalogueAuthorityResolution.Unique));
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        var report = result.Authority.Should().ContainSingle().Subject;
        report.Query.Should().Be(brand);
        report.ContentKind.Should().Be("TvShowEpisode");
        report.Resolution.Should().Be(CatalogueAuthorityResolution.Unique);
        report.ImdbId.Should().Be(imdbId);
        report.TmdbId.Should().Be(tmdbId);
        report.TvdbId.Should().Be(tvdbId);
        report.EpisodeIds.Should().BeEquivalentTo([first.Id, second.Id]);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a non-unique publisher has more programmes than the lookup limit, then those programmes are not sent to TMDB or TheTVDB, because a channel-sized set must not be submitted.")]
    public async Task too_many_channel_programmes_are_not_sent()
    {
        // Arrange
        var show = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var overLimit = CatalogueMigrateIdentifyProcessor.MaxSignalAuthorityLookups + 1;
        for (var i = 0; i < overLimit; i++)
        {
            var index = i;
            var episode = _fixture.CreateStoredEpisode(
                show,
                stored => stored.Title = $"{_fixture.CreateTitle()} {index.ToString(CultureInfo.InvariantCulture)}");
            if (i < 2)
            {
                EpisodeServicePresence.Upsert(
                    episode,
                    "bbcIplayer",
                    new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}"),
                    image: null);
            }

            _episodes.Seed(episode);
        }

        _podcasts.Seed(show);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                2,
                null,
                null,
                null,
                [show.Name],
                Resolution: CatalogueAuthorityResolution.SeveralProgrammes));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(
                It.Is<string>(query => query != show.Name),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("TMDB must not be called for programmes above the lookup limit."));
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        var report = result.Authority.Should().ContainSingle().Subject;
        report.Query.Should().Be(show.Name);
        report.Resolution.Should().Be(CatalogueAuthorityResolution.SeveralProgrammes);
        report.ImdbId.Should().BeNull();
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a TV title is not in the TV authorities and the same title is one film, then the report is that film, because a curator title with no series can still be a one-off.")]
    public async Task tv_title_missing_from_tv_authorities_uses_one_film()
    {
        // Arrange
        var show = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        SeedIplayerPair(show);
        var imdbId = ImdbId();
        var tmdbId = (int)(_fixture.CreateAppleId() % 1_000_000_000L);
        if (tmdbId == 0)
        {
            tmdbId = 1;
        }

        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                0,
                null,
                null,
                null,
                [],
                Resolution: CatalogueAuthorityResolution.NotInAuthority));
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewFilmAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                1,
                imdbId,
                tmdbId,
                null,
                [show.Name],
                Resolution: CatalogueAuthorityResolution.Unique));
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        var report = result.Authority.Should().ContainSingle().Subject;
        report.ContentKind.Should().Be("Film");
        report.Resolution.Should().Be(CatalogueAuthorityResolution.Unique);
        report.ImdbId.Should().Be(imdbId);
        report.TmdbId.Should().Be(tmdbId);
        report.TvdbId.Should().BeNull();
        _podcasts.SavedPodcasts.Should().BeEmpty();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when a four-letter YouTube publisher has no episodes, " +
        "then dry-run counts no News candidate, because the call-sign heuristic needs YouTube-only playables.")]
    public async Task four_letter_youtube_publisher_with_no_episodes_is_not_a_candidate()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = CreateFourLetterName();
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        _podcasts.Seed(podcast);
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        result.ExitCode.Should().Be(0);
        result.CandidateCount.Should().Be(0);
    }

    [Fact(DisplayName =
        "Catalogue migrate identify: when every episode is a part of the publisher title, then that title stays the programme and the part ids are kept, because the parts are one show.")]
    public async Task part_titles_keep_the_publisher_query_and_episode_ids()
    {
        // Arrange
        var show = _fixture.CreatePodcast(p =>
        {
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var first = _fixture.CreateStoredEpisode(show, episode => episode.Title = $"{show.Name} (Part 1)");
        var second = _fixture.CreateStoredEpisode(show, episode => episode.Title = $"{show.Name} (Part 2)");
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
        _podcasts.Seed(show);
        _episodes.Seed(first, second);
        _mocker.GetMock<ICatalogueAuthorityPreviewer>()
            .Setup(previewer => previewer.PreviewTvShowAsync(show.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueAuthorityPreview(
                show.Name,
                0,
                null,
                null,
                null,
                [],
                Resolution: CatalogueAuthorityResolution.NotInAuthority));
        var sut = _mocker.CreateInstance<CatalogueMigrateIdentifyProcessor>();

        // Act
        var result = await sut.Run(new CatalogueMigrateIdentifyRequest());

        // Assert
        var report = result.Authority.Should().ContainSingle().Subject;
        report.Query.Should().Be(show.Name);
        report.Resolution.Should().Be(CatalogueAuthorityResolution.NotInAuthority);
        report.ContentKind.Should().Be("TvShowEpisode");
        report.EpisodeIds.Should().BeEquivalentTo([first.Id, second.Id]);
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    private void SeedIplayerPair(Podcast podcast)
    {
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
        _podcasts.Seed(podcast);
        _episodes.Seed(first, second);
    }

    private string CreateFourLetterName()
    {
        var seed = _fixture.Create<int>() & int.MaxValue;
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var name = string.Create(4, seed + attempt, static (span, value) =>
            {
                var n = value;
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[Math.Abs(n) % 26];
                    n = HashCode.Combine(n, i);
                }
            });
            if (!CatalogueTvShowCanonicalNames.IsPublisherName(name)
                && !CatalogueNewsOrganisationAllowlist.Contains(name))
            {
                return name;
            }
        }

        return "QZXJ";
    }

    private string ImdbId() =>
        "tt" + _fixture.CreateAppleId().ToString(CultureInfo.InvariantCulture);
}
