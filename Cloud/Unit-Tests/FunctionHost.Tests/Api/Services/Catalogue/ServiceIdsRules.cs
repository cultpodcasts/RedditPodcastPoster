using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

public class ServiceIdsRules
{
    private const string ChannelId = "UCsyntheticChannel0000001";
    private const string OtherChannelId = "UCsyntheticChannel0000002";
    private const string PlaylistId = "PLsyntheticPlaylist000001";
    private const string OtherPlaylistId = "PLsyntheticPlaylist000002";
    private const string SpotifyShowId = "syntheticSpotifyShow00001";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private NewsOrganisation? _savedOrganisation;
    private TvShow? _savedShow;

    public ServiceIdsRules()
    {
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(o => _savedOrganisation = o)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(s => _savedShow = s)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns(AsyncEnumerable.Empty<NewsReport>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(AsyncEnumerable.Empty<TvShowEpisode>());
    }

    private NewsOrganisation Organisation(CatalogueIndexingSource? indexing = null)
    {
        var organisation = new NewsOrganisation("Synthetic Evening News") { Id = _fixture.CreateGuid(), Indexing = indexing };
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id)).ReturnsAsync(organisation);
        return organisation;
    }

    private async Task<NewsOrganisationUpdateResult> Patch(NewsOrganisation organisation, NewsOrganisationChangeRequest change) =>
        await _mocker.CreateInstance<NewsOrganisationService>()
            .UpdateAsync(new NewsOrganisationChangeRequestWrapper(organisation.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH news organisation youTubeChannelId on an outlet with no indexing block creates one with the trimmed id, " +
        "not auto-indexed, and GET returns it, so a curator can restore a channel the kind transfer dropped.")]
    public async Task sets_channel_id_on_outlet_without_indexing()
    {
        // Arrange
        var organisation = Organisation();

        // Act
        var result = await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = $"  {ChannelId} " });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _savedOrganisation!.Indexing!.YouTubeChannelId.Should().Be(ChannelId);
        _savedOrganisation.Indexing.IsAutoIndexed.Should().BeFalse();
        var dto = _savedOrganisation.ToDto();
        dto.YouTubeChannelId.Should().Be(ChannelId);
        dto.SpotifyId.Should().BeEmpty();
        dto.AppleId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Service ids on an outlet migrated with discovery settings change only those ids; the other indexing settings are kept.")]
    public async Task keeps_other_indexing_settings()
    {
        // Arrange
        var organisation = Organisation(new CatalogueIndexingSource
        {
            IndexAllEpisodes = true, YouTubeChannelId = ChannelId, SpotifyId = SpotifyShowId, TitleRegex = "^Synthetic"
        });

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = OtherChannelId, AppleId = 123456789 });

        // Assert
        var indexing = _savedOrganisation!.Indexing!;
        indexing.YouTubeChannelId.Should().Be(OtherChannelId);
        indexing.AppleId.Should().Be(123456789);
        indexing.SpotifyId.Should().Be(SpotifyShowId);
        indexing.IndexAllEpisodes.Should().BeTrue();
        indexing.TitleRegex.Should().Be("^Synthetic");
    }

    [Fact(DisplayName =
        "PATCH news organisation episodeIncludeTitleRegex non-empty: IndexAllEpisodes is cleared and GET returns the pattern, because include-title filtering only runs when Index All is off.")]
    public async Task non_empty_include_title_regex_clears_index_all()
    {
        // Arrange
        var organisation = Organisation(new CatalogueIndexingSource
            { IndexAllEpisodes = true, YouTubeChannelId = ChannelId });
        var pattern = _fixture.CreateShortTitle();

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest { EpisodeIncludeTitleRegex = pattern });

        // Assert
        var indexing = _savedOrganisation!.Indexing!;
        indexing.EpisodeIncludeTitleRegex.Should().Be(pattern);
        indexing.IndexAllEpisodes.Should().BeFalse();
        indexing.IsAutoIndexed.Should().BeTrue();
        var dto = _savedOrganisation.ToDto();
        dto.EpisodeIncludeTitleRegex.Should().Be(pattern);
        dto.IndexAllEpisodes.Should().BeFalse();
    }

    [Fact(DisplayName =
        "PATCH news organisation indexAllEpisodes true: EpisodeIncludeTitleRegex is cleared, because Index All skips the include filter.")]
    public async Task enabling_index_all_clears_include_title_regex()
    {
        // Arrange
        var organisation = Organisation(new CatalogueIndexingSource
        {
            IndexAllEpisodes = false,
            YouTubeChannelId = ChannelId,
            EpisodeIncludeTitleRegex = _fixture.CreateShortTitle()
        });

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest { IndexAllEpisodes = true });

        // Assert
        var indexing = _savedOrganisation!.Indexing!;
        indexing.IndexAllEpisodes.Should().BeTrue();
        indexing.EpisodeIncludeTitleRegex.Should().BeNull();
        _savedOrganisation.ToDto().EpisodeIncludeTitleRegex.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Like the podcast edit API, \"\" clears youTubeChannelId and spotifyId (stored as null, never an empty string, " +
        "because blank indexing values are absent in Cosmos), and nullAppleId: true clears appleId even when appleId is also sent.")]
    public async Task clears_follow_podcast_rules()
    {
        // Arrange
        var organisation = Organisation(new CatalogueIndexingSource
            { YouTubeChannelId = ChannelId, SpotifyId = SpotifyShowId, AppleId = 42 });

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest
            { YouTubeChannelId = "", SpotifyId = "", AppleId = 7, NullAppleId = true });

        // Assert
        var indexing = _savedOrganisation!.Indexing!;
        indexing.YouTubeChannelId.Should().BeNull();
        indexing.SpotifyId.Should().BeNull();
        indexing.AppleId.Should().BeNull();
    }

    private static JsonObject StoredIndexing(CatalogueIndexingSource indexing) =>
        (JsonObject) JsonSerializer.SerializeToNode(
            indexing, new JsonSerializerOptionsProvider().GetJsonSerializerOptions())!;

    [Fact(DisplayName =
        "Setting only youTubeChannelId on an outlet with no indexing block stores only that id: no \"\" spotifyId, " +
        "youTubePlaylistId or regexes are written, because unset ids and regexes are omitted like any other unset field.")]
    public async Task new_block_holds_only_the_set_id()
    {
        // Arrange
        var organisation = Organisation();

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = ChannelId });

        // Assert
        var stored = StoredIndexing(_savedOrganisation!.Indexing!);
        stored.Select(pair => pair.Key).Should()
            .BeEquivalentTo("hasBundledEpisodes", "indexAllEpisodes", "youTubeChannelId");
        stored["youTubeChannelId"]!.GetValue<string>().Should().Be(ChannelId);
    }

    [Fact(DisplayName =
        "A whitespace youTubeChannelId, spotifyId or youTubePlaylistId clears the stored value (null, omitted), " +
        "and the other ids are kept.")]
    public async Task whitespace_clears_to_null()
    {
        // Arrange
        var organisation = Organisation(new CatalogueIndexingSource
            { YouTubeChannelId = ChannelId, SpotifyId = SpotifyShowId, YouTubePlaylistId = PlaylistId, AppleId = 42 });

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest
            { YouTubeChannelId = "   ", SpotifyId = "\t", YouTubePlaylistId = " " });

        // Assert
        var indexing = _savedOrganisation!.Indexing!;
        indexing.YouTubeChannelId.Should().BeNull();
        indexing.SpotifyId.Should().BeNull();
        indexing.YouTubePlaylistId.Should().BeNull();
        indexing.YouTubePlaylistIdHistory.Should().ContainSingle().Which.Id.Should().Be(PlaylistId);
        var stored = StoredIndexing(indexing);
        stored.ContainsKey("youTubeChannelId").Should().BeFalse();
        stored.ContainsKey("spotifyId").Should().BeFalse();
        stored.ContainsKey("youTubePlaylistId").Should().BeFalse();
        stored["appleId"]!.GetValue<long>().Should().Be(42);
    }

    [Fact(DisplayName =
        "An outlet saved with \"\" ids and regexes (before blanks were normalised) is cleaned on the next PATCH: " +
        "the blanks are dropped and only the set values are written.")]
    public async Task legacy_blank_values_are_dropped_on_save()
    {
        // Arrange
        var legacy = JsonSerializer.Deserialize<CatalogueIndexingSource>(
            """
            {"hasBundledEpisodes":false,"indexAllEpisodes":false,"spotifyId":"","youTubeChannelId":"",
             "youTubePlaylistId":"","titleRegex":"","descriptionRegex":"","episodeMatchRegex":"",
             "episodeIncludeTitleRegex":""}
            """,
            new JsonSerializerOptionsProvider().GetJsonSerializerOptions())!;
        var organisation = Organisation(legacy);

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest { YouTubeChannelId = ChannelId });

        // Assert
        StoredIndexing(_savedOrganisation!.Indexing!).Select(pair => pair.Key).Should()
            .BeEquivalentTo("hasBundledEpisodes", "indexAllEpisodes", "youTubeChannelId");
    }

    [Fact(DisplayName =
        "Clearing ids on an outlet with no indexing block does not create an empty one, and a PATCH without service ids leaves indexing untouched.")]
    public async Task no_empty_indexing_block()
    {
        // Arrange
        var bare = Organisation();
        var indexed = new CatalogueIndexingSource { YouTubeChannelId = ChannelId };

        // Act
        await Patch(bare, new NewsOrganisationChangeRequest { YouTubeChannelId = "", SpotifyId = " ", NullAppleId = true });
        var bareIndexing = _savedOrganisation!.Indexing;
        var other = Organisation(indexed);
        await Patch(other, new NewsOrganisationChangeRequest { Description = "Synthetic" });

        // Assert
        bareIndexing.Should().BeNull();
        _savedOrganisation!.Indexing.Should().BeSameAs(indexed);
        indexed.YouTubeChannelId.Should().Be(ChannelId);
    }

    [Fact(DisplayName =
        "youTubePlaylistId follows YouTubePlaylistIdChange: a replaced id goes to youTubePlaylistIdHistory, the same id is a no-op, and GET returns both.")]
    public async Task playlist_id_keeps_history()
    {
        // Arrange
        var organisation = Organisation(new CatalogueIndexingSource { YouTubePlaylistId = PlaylistId });

        // Act
        await Patch(organisation, new NewsOrganisationChangeRequest { YouTubePlaylistId = $" {OtherPlaylistId} " });
        await Patch(organisation, new NewsOrganisationChangeRequest { YouTubePlaylistId = OtherPlaylistId });

        // Assert
        var indexing = _savedOrganisation!.Indexing!;
        indexing.YouTubePlaylistId.Should().Be(OtherPlaylistId);
        indexing.YouTubePlaylistIdHistory.Should().ContainSingle().Which.Id.Should().Be(PlaylistId);
        var dto = _savedOrganisation.ToDto();
        dto.YouTubePlaylistId.Should().Be(OtherPlaylistId);
        dto.YouTubePlaylistIdHistory!.Single().Id.Should().Be(PlaylistId);
    }

    [Fact(DisplayName =
        "PATCH TV show takes the same service ids alongside the IMDb id, stores them under indexing, and GET returns them.")]
    public async Task tv_show_service_ids()
    {
        // Arrange
        var imdbId = _fixture.CreateImdbId();
        var show = new TvShow("Synthetic Drama") { Id = _fixture.CreateGuid() };
        _mocker.GetMock<ITvShowRepository>().Setup(r => r.GetTvShow(show.Id)).ReturnsAsync(show);

        // Act
        var result = await _mocker.CreateInstance<TvShowService>().UpdateAsync(
            new TvShowChangeRequestWrapper(show.Id, new TvShowChangeRequest
            {
                ImdbId = imdbId,
                YouTubeChannelId = ChannelId,
                SpotifyId = SpotifyShowId,
                AppleId = 99
            }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _savedShow!.Indexing!.YouTubeChannelId.Should().Be(ChannelId);
        StoredIndexing(_savedShow.Indexing).Select(pair => pair.Key).Should().BeEquivalentTo(
            "hasBundledEpisodes", "indexAllEpisodes", "youTubeChannelId", "spotifyId", "appleId");
        var dto = _savedShow.ToDto();
        dto.YouTubeChannelId.Should().Be(ChannelId);
        dto.SpotifyId.Should().Be(SpotifyShowId);
        dto.AppleId.Should().Be(99);
        dto.ImdbId.Should().Be(imdbId);
    }

    [Fact(DisplayName =
        "The PATCH body uses the podcast edit API's wire names (youTubeChannelId, youTubePlaylistId, spotifyId, appleId, nullAppleId) and they reach the model.")]
    public void wire_names()
    {
        // Arrange
        const string json = """
            {"youTubeChannelId":"UCsyntheticChannel0000001","youTubePlaylistId":"PLsyntheticPlaylist000001",
             "spotifyId":"syntheticSpotifyShow00001","appleId":5,"nullAppleId":false}
            """;

        // Act
        var news = JsonSerializer.Deserialize<global::Api.Dtos.NewsOrganisationChangeRequest>(json)!.ToModel();
        var tv = JsonSerializer.Deserialize<global::Api.Dtos.TvShowChangeRequest>(json)!.ToModel();

        // Assert
        foreach (var change in new ServiceIdsChange[] { news, tv })
        {
            change.YouTubeChannelId.Should().Be(ChannelId);
            change.YouTubePlaylistId.Should().Be(PlaylistId);
            change.SpotifyId.Should().Be(SpotifyShowId);
            change.AppleId.Should().Be(5);
            change.NullAppleId.Should().BeFalse();
        }
    }
}
