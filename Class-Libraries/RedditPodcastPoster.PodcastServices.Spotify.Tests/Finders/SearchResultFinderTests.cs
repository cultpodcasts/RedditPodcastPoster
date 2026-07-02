using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Finders;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Finders;

public class SearchResultFinderTests
{
    private readonly SearchResultFinder _sut = new(NullLogger<SearchResultFinder>.Instance);

    [Fact]
    public void FindMatchingEpisodeByLength_WhenTitlesDifferButDurationIsUnique_AcceptsWithoutTitleMatch()
    {
        var episodeLength = TimeSpan.FromMinutes(62);
        var episodes = new List<SimpleEpisode>
        {
            new()
            {
                Id = "577DaJslYV1BZUXasvwLBT",
                Name = "My Family Was America's Most Dangerous Cult",
                DurationMs = (int)episodeLength.TotalMilliseconds,
                ReleaseDate = "2026-07-02"
            },
            new()
            {
                Id = "other-episode",
                Name = "Different Episode",
                DurationMs = (int)TimeSpan.FromMinutes(30).TotalMilliseconds,
                ReleaseDate = "2026-07-02"
            }
        };

        var result = _sut.FindMatchingEpisodeByLength(
            "\"I Grew Up in a Murder Cult\" Cult Survivor Reveals What It's Like To Grow Up Inside It",
            episodeLength,
            episodes,
            acceptUniqueDurationWithoutTitleMatch: true);

        result.Should().NotBeNull();
        result!.Id.Should().Be("577DaJslYV1BZUXasvwLBT");
    }
}
