using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Catalogue.Identity;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Tmdb.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowFieldUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<TvShowEpisode> _episodes = [];
    private TvShow? _saved;
    private TvShowEpisode[]? _savedEpisodes;

    public TvShowFieldUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>()
            .SetupSaveViaReplace(show => _saved = show);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(() => _episodes.ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<IEnumerable<TvShowEpisode>>()))
            .Callback<IEnumerable<TvShowEpisode>>(episodes => _savedEpisodes = episodes.ToArray())
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TmdbShowMetadata?)null);
    }

    private TvShow CreateShow(Action<TvShow>? customise = null)
    {
        var show = new TvShow("Synthetic Cult Docuseries") { Id = _fixture.CreateGuid(), TwitterHandle = "@keep" };
        customise?.Invoke(show);
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);
        return show;
    }

    private Task<TvShowUpdateResult> Update(TvShow show, TvShowChangeRequest change) =>
        _mocker.CreateInstance<TvShowService>()
            .UpdateAsync(new TvShowChangeRequestWrapper(show.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH TV show sets the publisher fields it is given with the news-organisation rules and keeps omitted ones, because TV shows share the publisher editor.")]
    public async Task update_sets_publisher_fields()
    {
        // Arrange
        var show = CreateShow(s => { s.HashTag = "#Old"; s.KnownTerms = ["old"]; s.DefaultSubject = "Synthetic Old"; });

        // Act
        var result = await Update(show, new TvShowChangeRequest
        {
            BlueskyHandle = " synthetic.bsky.social ",
            HashTag = "",
            EnrichmentHashTags = [" #A "],
            KnownTerms = [],
            DefaultSubject = "",
            IgnoredSubjects = ["Synthetic Ignored"],
            IgnoredAssociatedSubjects = ["Synthetic Assoc"]
        });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.TwitterHandle.Should().Be("@keep");
        _saved.BlueskyHandle.Should().Be("synthetic.bsky.social");
        _saved.HashTag.Should().BeNull();
        _saved.EnrichmentHashTags.Should().Equal("#A");
        _saved.KnownTerms.Should().BeNull();
        _saved.DefaultSubject.Should().BeNull();
        _saved.IgnoredSubjects.Should().Equal("Synthetic Ignored");
        _saved.IgnoredAssociatedSubjects.Should().Equal("Synthetic Assoc");
    }

    [Fact(DisplayName =
        "PATCH TV show search terms and language copies them onto its episodes and saves the changed ones, because episodes denormalise them for search.")]
    public async Task update_propagates_search_terms_and_language()
    {
        // Arrange
        var show = CreateShow();
        var stale = new TvShowEpisode("Synthetic Episode 1") { Id = _fixture.CreateGuid() };
        stale.SetTvShowProperties(show);
        _episodes.Add(stale);

        // Act
        await Update(show, new TvShowChangeRequest { SearchTerms = "synthetic terms", Language = "fr" });

        // Assert
        _saved!.SearchTerms.Should().Be("synthetic terms");
        _saved.Language.Should().Be("fr");
        _savedEpisodes.Should().ContainSingle().Which.Should().BeSameAs(stale);
        stale.PublisherSearchTerms.Should().Be("synthetic terms");
        stale.PublisherLanguage.Should().Be("fr");
    }

    [Fact(DisplayName =
        "PATCH TV show with only a handle never reads its episodes, because only search terms and language are denormalised.")]
    public async Task update_handle_only_skips_episodes()
    {
        // Arrange
        var show = CreateShow();

        // Act
        await Update(show, new TvShowChangeRequest { TwitterHandle = "@synthetic" });

        // Assert
        _saved!.TwitterHandle.Should().Be("@synthetic");
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(r => r.GetByTvShowId(It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "PATCH TV show with English as the language stores null and keeps the IMDb id, because English is the default and canonical ids are edited separately.")]
    public async Task update_english_language_is_null_and_canonical_kept()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var show = CreateShow(s => { s.Language = "de"; s.ImdbId = imdbId; });

        // Act
        await Update(show, new TvShowChangeRequest { Language = "en-GB" });

        // Assert
        _saved!.Language.Should().BeNull();
        _saved.ImdbId.Should().Be(imdbId);
    }

    [Fact(DisplayName =
        "GET TV show DTO carries the publisher fields as well as canonical ids, because the curator UI edits both.")]
    public void dto_has_publisher_fields()
    {
        // Arrange
        var tvdbId = _fixture.CreateTvdbId();
        var show = CreateShow(s => { s.SearchTerms = "synthetic"; s.HashTag = "#Tag"; s.Removed = true; s.TvdbId = tvdbId; });

        // Act
        var dto = show.ToDto();

        // Assert
        dto.Id.Should().Be(show.Id);
        dto.Name.Should().Be("Synthetic Cult Docuseries");
        dto.SearchTerms.Should().Be("synthetic");
        dto.HashTag.Should().Be("#Tag");
        dto.TwitterHandle.Should().Be("@keep");
        dto.Removed.Should().BeTrue();
        dto.TvdbId.Should().Be(tvdbId);
    }

    [Fact(DisplayName =
        "An empty TV-show publisher is filled from TMDB (the first named network) when the show has an identity id " +
        "and this PATCH did not set or clear publisher (RPP #1436).")]
    public async Task empty_publisher_is_filled_from_tmdb()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var show = CreateShow(s => s.TmdbId = tmdbId);
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbShowMetadata(tmdbId, "HBO"));

        // Act
        var result = await Update(show, new TvShowChangeRequest { TwitterHandle = "@synthetic" });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.PublisherName.Should().Be("HBO");
        _saved.TwitterHandle.Should().Be("@synthetic");
    }

    [Fact(DisplayName =
        "Clearing a TV show's publisher in the same PATCH leaves it empty even when TMDB has a network (RPP #1436).")]
    public async Task a_publisher_cleared_in_this_patch_is_not_refilled()
    {
        // Arrange
        var tmdbId = _fixture.CreateTmdbId();
        var show = CreateShow(s =>
        {
            s.TmdbId = tmdbId;
            s.PublisherName = "Old Network";
        });
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowAsync(tmdbId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbShowMetadata(tmdbId, "HBO"));

        // Act
        var result = await Update(show, new TvShowChangeRequest { PublisherName = "" });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.PublisherName.Should().BeEmpty();
        _mocker.GetMock<IIdentityMetadataEnricher>().Verify(
            e => e.FetchTvShowAsync(It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "Clearing a TV show's TMDB id in the same PATCH leaves it cleared even when an IMDb id lookup finds that " +
        "TMDB id, and an empty publisher is still filled.")]
    public async Task a_tmdb_id_cleared_in_this_patch_is_not_restored()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var existingTmdbId = _fixture.CreateTmdbId();
        var foundTmdbId = existingTmdbId + 1;
        var show = CreateShow(s =>
        {
            s.ImdbId = imdbId;
            s.TmdbId = existingTmdbId;
        });
        _mocker.GetMock<IIdentityMetadataEnricher>()
            .Setup(e => e.FetchTvShowAsync(null, imdbId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TmdbShowMetadata(foundTmdbId, "HBO"));

        // Act
        var result = await Update(show, new TvShowChangeRequest { TmdbId = 0 });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.TmdbId.Should().BeNull();
        _saved.ImdbId.Should().Be(imdbId);
        _saved.PublisherName.Should().Be("HBO");
    }

}
