using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1484: Azure search and D1 key off an episode's approval status: an episode awaiting approval or rejected is kept
/// out whatever its removed flag says; an approved one (or one with no status) is indexed as usual.
/// </summary>
public class SearchApprovalStatusRules
{
    private readonly DomainTestFixture _fixture = new();

    [Theory(DisplayName =
        "An episode awaiting approval or rejected is excluded from Azure search and D1 even with removed cleared; an " +
        "approved one or an ordinary one is included (#1484).")]
    [InlineData(ApprovalStatus.AwaitingApproval, true)]
    [InlineData(ApprovalStatus.Rejected, true)]
    [InlineData(ApprovalStatus.Approved, false)]
    [InlineData(null, false)]
    public void search_eligibility_keys_off_approval_status(ApprovalStatus? status, bool excluded)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        episode.Removed = false;
        episode.ParentRemoved = null;
        episode.ApprovalStatus = status;

        // Act
        var azure = EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode);
        var d1 = D1SearchEligibility.ShouldExclude(podcast, episode);

        // Assert
        azure.Should().Be(excluded);
        d1.Should().Be(excluded);
    }
}
