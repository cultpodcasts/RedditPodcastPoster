using Api.Models;
using Api.Services;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.YouTube;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class YouTubeAlternateNewIdsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A padded additional YouTube id is trimmed before the ownership check, because the id that will be stored is the trimmed one and a padded copy must not skip a holder (RPP #1526).")]
    public void new_ids_trims_youtube_id_before_the_ownership_check()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var addedId = _fixture.CreateYouTubeId();
        var padded = $" {addedId} ";

        // Act
        var ids = YouTubeAlternateChangeApplier.NewIds(
            episode,
            [new YouTubeAlternateChange(padded, _fixture.DefaultYouTubeUrl(addedId).ToString(), null, null)],
            episode.Ids!.YouTube);

        // Assert
        ids.Should().Equal(addedId);
    }

    [Fact(DisplayName =
        "A padded additional YouTube id that trims to one this item already holds is not a new id, because the ownership check must see the same id validation stores (RPP #1526).")]
    public void new_ids_skips_a_padded_id_that_trims_to_one_already_held()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var heldId = _fixture.CreateYouTubeId();
        episode.AdditionalYouTube =
        [
            new YouTubeAlternate
            {
                YouTubeId = heldId,
                Url = _fixture.DefaultYouTubeUrl(heldId),
                AddedAt = DateTime.UtcNow
            }
        ];

        // Act
        var ids = YouTubeAlternateChangeApplier.NewIds(
            episode,
            [new YouTubeAlternateChange($" {heldId} ", _fixture.DefaultYouTubeUrl(heldId).ToString(), null, null)],
            episode.Ids!.YouTube);

        // Assert
        ids.Should().BeEmpty();
    }
}
