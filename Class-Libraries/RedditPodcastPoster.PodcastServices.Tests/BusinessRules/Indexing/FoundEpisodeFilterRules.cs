using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class FoundEpisodeFilterRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Found-episode include-title regex: when an episode title matches the podcast include pattern, then it is kept, because catalogue intake must honour EpisodeIncludeTitleRegex.")]
    public void matching_title_is_kept()
    {
        // Arrange
        var includeToken = _fixture.CreateTitle(1);
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.EpisodeIncludeTitleRegex = includeToken;
        });
        var matching = _fixture.CreateStoredEpisode(podcast, e => e.Title = $"{includeToken} {_fixture.CreateTitle()}");
        var other = _fixture.CreateStoredEpisode(podcast, e => e.Title = _fixture.CreateTitle());
        var sut = new FoundEpisodeFilter(NullLogger<FoundEpisodeFilter>.Instance);

        // Act
        var reduced = sut.ReduceEpisodes(podcast, [matching, other]);

        // Assert
        reduced.Should().ContainSingle().Which.Id.Should().Be(matching.Id);
    }

    [Fact(DisplayName =
        "Found-episode include-title regex: when no episode titles match the podcast include pattern, then the result is empty, because non-matching catalogue rows must be eliminated.")]
    public void non_matching_titles_are_eliminated()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.EpisodeIncludeTitleRegex = _fixture.CreateTitle(1);
        });
        var episodes = new List<Episode>
        {
            _fixture.CreateStoredEpisode(podcast),
            _fixture.CreateStoredEpisode(podcast)
        };
        var sut = new FoundEpisodeFilter(NullLogger<FoundEpisodeFilter>.Instance);

        // Act
        var reduced = sut.ReduceEpisodes(podcast, episodes);

        // Assert
        reduced.Should().BeEmpty();
    }
}
