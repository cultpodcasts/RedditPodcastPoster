using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Categorisers;

/// <summary>
/// SubmitUrl / discovery-curation URL-authority Resolve must short-circuit on existing episode URLs,
/// resolve by Spotify episode id, and throw when hydrate misses.
/// </summary>
public class SpotifyUrlCategoriserUrlAuthorityRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When podcast episodes already contain Urls.Spotify equal to the submit URL, Resolve returns that episode without FindEpisode " +
        "because URL authority must reuse the curated row rather than re-hitting Spotify.")]
    public async Task Existing_spotify_url_short_circuits_without_resolver()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.SpotifyId = _fixture.CreateSpotifyId());
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var url = episode.Urls.Spotify!;
        var resolver = new Mock<ISpotifyEpisodeResolver>(MockBehavior.Strict);
        var sut = CreateSut(resolver.Object);

        // Act
        var result = await sut.Resolve(podcast, [episode], url, new IndexingContext());

        // Assert
        result.EpisodeId.Should().Be(episode.SpotifyId);
        result.EpisodeTitle.Should().Be(episode.Title);
        result.Url.Should().Be(url);
        resolver.Verify(
            x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When the URL is a new Spotify episode link, Resolve calls FindEpisode with Create(episodeId) and maps FullEpisode fields " +
        "because submit/curation must hydrate authority from the Spotify episode id in the path.")]
    public async Task New_episode_url_resolves_via_find_episode_by_id()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var url = _fixture.DefaultSpotifyUrl(episodeId);
        var title = _fixture.CreateTitle();
        var fullEpisode = CreateFullEpisode(episodeId, title);
        FindSpotifyEpisodeRequest? captured = null;
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .Callback<FindSpotifyEpisodeRequest, IndexingContext, Func<SimpleEpisode, bool>?>(
                (request, _, _) => captured = request)
            .ReturnsAsync(new FindEpisodeResponse(fullEpisode));
        var sut = CreateSut(resolver.Object);
        var expectedDirectIdRequest = FindSpotifyEpisodeRequestFactory.Create(episodeId);

        // Act
        var result = await sut.Resolve(null, [], url, new IndexingContext());

        // Assert
        captured.Should().NotBeNull();
        captured!.EpisodeSpotifyId.Should().Be(expectedDirectIdRequest.EpisodeSpotifyId);
        captured.PodcastSpotifyId.Should().BeEmpty();
        captured.PodcastName.Should().BeEmpty();
        result.EpisodeId.Should().Be(episodeId);
        result.EpisodeTitle.Should().Be(title);
        result.ShowId.Should().Be(fullEpisode.Show.Id);
    }

    [Fact(DisplayName =
        "When FindEpisode returns a null FullEpisode for a parseable episode URL, Resolve throws InvalidOperationException naming the episode id " +
        "because submit must fail closed rather than invent an unresolved Spotify row.")]
    public async Task Missing_full_episode_throws_with_episode_id()
    {
        // Arrange
        var episodeId = _fixture.CreateSpotifyId();
        var url = _fixture.DefaultSpotifyUrl(episodeId);
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .ReturnsAsync(new FindEpisodeResponse(null));
        var sut = CreateSut(resolver.Object);

        // Act
        var act = () => sut.Resolve(null, [], url, new IndexingContext());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{episodeId}*");
    }

    [Fact(DisplayName =
        "KNOWN: When the URL has no /episode/{id} segment, GetEpisodeId returns empty string (not null), so Resolve skips the null-id throw " +
        "and proceeds to FindEpisode — characterize current routing, do not fix in this tests-only PR.")]
    public async Task Non_episode_url_uses_empty_id_path_not_null_throw()
    {
        // Arrange
        // KNOWN: SpotifyIdResolver.GetEpisodeId returns Groups[].Value (empty), while Resolve null-checks for throw.
        var showId = _fixture.CreateSpotifyId();
        var url = new Uri($"https://open.spotify.com/show/{showId}");
        FindSpotifyEpisodeRequest? captured = null;
        var resolver = new Mock<ISpotifyEpisodeResolver>();
        resolver
            .Setup(x => x.FindEpisode(
                It.IsAny<FindSpotifyEpisodeRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<Func<SimpleEpisode, bool>?>()))
            .Callback<FindSpotifyEpisodeRequest, IndexingContext, Func<SimpleEpisode, bool>?>(
                (request, _, _) => captured = request)
            .ReturnsAsync(new FindEpisodeResponse(null));
        var sut = CreateSut(resolver.Object);

        // Act
        var act = () => sut.Resolve(null, [], url, new IndexingContext());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Could not find item with spotify-id*");
        captured.Should().NotBeNull();
        captured!.EpisodeSpotifyId.Should().BeEmpty();
    }

    private FullEpisode CreateFullEpisode(string episodeId, string title)
    {
        var showId = _fixture.CreateSpotifyId();
        return new FullEpisode
        {
            Id = episodeId,
            Name = title,
            HtmlDescription = $"<p>{_fixture.CreateTitle()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            Explicit = false,
            ExternalUrls = new Dictionary<string, string>
            {
                ["spotify"] = _fixture.DefaultSpotifyUrl(episodeId).ToString()
            },
            Images = [],
            Show = new SimpleShow
            {
                Id = showId,
                Name = _fixture.CreateTitle(),
                Description = _fixture.CreateTitle(),
                Publisher = _fixture.CreateTitle()
            }
        };
    }

    private static SpotifyUrlCategoriser CreateSut(ISpotifyEpisodeResolver resolver) =>
        new(
            resolver,
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            NullLogger<SpotifyUrlCategoriser>.Instance);
}
