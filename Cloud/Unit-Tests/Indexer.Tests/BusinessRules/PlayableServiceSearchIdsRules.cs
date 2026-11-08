using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Search.Indexing;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// Pull/push parity for the Spotify and Apple search ids of news, TV and film documents. The pull SQL
/// (<see cref="PlayableSearchSql"/>) and the curator-edit push (<see cref="CataloguePlayableSearchDocument"/>) must
/// give the same three values for the same <c>services</c> URLs: the push is a merge-upload, so a null there blanks
/// what the pull wrote. Each case is that string contract (the C# twin of the SQL). The last-/id rows, including the
/// reversed cut, were not executed against Cosmos.
/// </summary>
public class PlayableServiceSearchIdsRules
{
    private readonly DomainTestFixture _fixture = new();

    public static TheoryData<string?, string?, string?, string?, string?> ServiceUrlCases => new()
    {
        // spotify url, apple url, spotifyId, appleId, podcastAppleId
        {
            "https://open.spotify.com/episode/0uKsy2FKD8ajajsmJ1p437",
            "https://podcasts.apple.com/gb/podcast/synthetic-show/id1630722986?i=1000798590779",
            "0uKsy2FKD8ajajsmJ1p437", "1000798590779", "1630722986"
        },
        {
            "https://open.spotify.com/episode/4rOoJ6Egrf8K2IrywzwOMk?si=abc123",
            "https://podcasts.apple.com/us/podcast/synthetic-show/id1000000001?i=1000000000002&uo=4",
            "4rOoJ6Egrf8K2IrywzwOMk", "1000000000002", "1000000001"
        },
        {
            null,
            "https://podcasts.apple.com/gb/podcast/identity-crisis/id123?i=456",
            null, "456", "123"
        },
        {
            null,
            "https://podcasts.apple.com/us/podcast/idaho-evening-news/id987654?i=111",
            null, "111", "987654"
        },
        {
            null,
            "https://podcasts.apple.com/gb/podcast/id555?i=666",
            null, "666", "555"
        },
        {
            "https://open.spotify.com/show/2mTUnDkuKUkhiueKcVWoP0",
            "https://podcasts.apple.com/gb/podcast/synthetic-show/id1630722986",
            null, null, null
        },
        { null, null, null, null, null }
    };

    private static Dictionary<string, ServiceLink> Services(string? spotifyUrl, string? appleUrl, string youTubeId)
    {
        var services = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.YouTube] = new() { Url = new Uri($"https://www.youtube.com/watch?v={youTubeId}") }
        };
        if (spotifyUrl is not null)
        {
            services[ServiceKeys.Spotify] = new ServiceLink { Url = new Uri(spotifyUrl) };
        }

        if (appleUrl is not null)
        {
            services[ServiceKeys.Apple] = new ServiceLink { Url = new Uri(appleUrl) };
        }

        return services;
    }

    [Theory(DisplayName =
        "Service ids read from services URLs give the value the pull SQL gives: the Spotify episode id (query " +
        "dropped), the Apple ?i= id (later & parameters dropped), the Apple show id from the last /id segment " +
        "(slugs starting with \"id\" included), and null for show-level or missing URLs.")]
    [MemberData(nameof(ServiceUrlCases))]
    public void service_ids_follow_the_pull_sql_contract(
        string? spotifyUrl, string? appleUrl, string? spotifyId, string? appleId, string? podcastAppleId)
    {
        // Arrange
        var services = Services(spotifyUrl, appleUrl, _fixture.CreateYouTubeId());

        // Act
        var ids = PlayableServiceSearchIds.From(services);

        // Assert
        ids.Should().Be((spotifyId, appleId, podcastAppleId));
    }

    [Theory(DisplayName =
        "A curator-edit push of a news report carries the same spotifyId, appleId and podcastAppleId as the pull " +
        "SQL, because a merge-upload with nulls would blank the ids and the Spotify/Apple icons.")]
    [MemberData(nameof(ServiceUrlCases))]
    public void news_report_push_matches_pull(
        string? spotifyUrl, string? appleUrl, string? spotifyId, string? appleId, string? podcastAppleId)
    {
        // Arrange
        var report = new NewsReport(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            NewsOrganisationId = _fixture.CreateGuid(),
            NewsOrganisationName = _fixture.CreateTitle(),
            Services = Services(spotifyUrl, appleUrl, _fixture.CreateYouTubeId())
        };

        // Act
        var record = CataloguePlayableSearchDocument.For(report);

        // Assert
        (record.SpotifyId, record.AppleId, record.PodcastAppleId).Should().Be((spotifyId, appleId, podcastAppleId));
    }

    [Fact(DisplayName =
        "A curator-edit push of a TV show episode carries the Spotify and Apple ids from its services URLs, the " +
        "same as the TV pull SQL.")]
    public void tv_show_episode_push_matches_pull()
    {
        // Arrange
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            TvShowId = _fixture.CreateGuid(),
            TvShowName = _fixture.CreateTitle(),
            Services = Services(
                "https://open.spotify.com/episode/0uKsy2FKD8ajajsmJ1p437",
                "https://podcasts.apple.com/gb/podcast/identity-crisis/id123?i=456",
                _fixture.CreateYouTubeId())
        };

        // Act
        var record = CataloguePlayableSearchDocument.For(episode);

        // Assert
        record.SpotifyId.Should().Be("0uKsy2FKD8ajajsmJ1p437");
        record.AppleId.Should().Be("456");
        record.PodcastAppleId.Should().Be("123");
    }

    [Fact(DisplayName =
        "A YouTube-only film push has null Spotify and Apple ids, and a film with an Apple link gets its ids, so " +
        "films follow the same services rule as news and TV.")]
    public void film_push_reads_ids_from_services()
    {
        // Arrange
        var youTubeOnly = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Services = Services(null, null, _fixture.CreateYouTubeId())
        };
        var withApple = new Film(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Services = Services(null, "https://podcasts.apple.com/gb/podcast/synthetic-show/id42?i=43",
                _fixture.CreateYouTubeId())
        };

        // Act
        var youTubeOnlyRecord = CataloguePlayableSearchDocument.For(youTubeOnly);
        var withAppleRecord = CataloguePlayableSearchDocument.For(withApple);

        // Assert
        (youTubeOnlyRecord.SpotifyId, youTubeOnlyRecord.AppleId, youTubeOnlyRecord.PodcastAppleId)
            .Should().Be(((string?)null, (string?)null, (string?)null));
        (withAppleRecord.AppleId, withAppleRecord.PodcastAppleId).Should().Be(("43", "42"));
    }
}
