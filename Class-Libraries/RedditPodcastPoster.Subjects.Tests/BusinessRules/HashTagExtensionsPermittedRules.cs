using FluentAssertions;
using RedditPodcastPoster.Subjects.Extensions;

namespace RedditPodcastPoster.Subjects.Tests.BusinessRules;

public class HashTagExtensionsPermittedRules
{
    [Fact(DisplayName =
        "ToHashTags drops tokens that are not valid on both X and Bluesky, because those HashTags must not be accepted into the posting model.")]
    public void to_hash_tags_filters_non_permitted_tokens()
    {
        // Arrange
        const string stored = "#Cult #Q&A Some-Tag C++";

        // Act
        var result = stored.ToHashTags().ToArray();

        // Assert
        result.Should().ContainSingle().Which.Tag.Should().Be("#Cult");
    }

    [Fact(DisplayName =
        "FromEnrichmentHashTagsToHashTags keeps multi-word phrases whose whitespace-compacted body is a permitted platform hashtag, and drops phrases with dash, ampersand, or plus.")]
    public void from_enrichment_keeps_multi_word_and_drops_symbol_tags()
    {
        // Arrange
        string[] enrichment = ["Alpha Beta", "Some-Tag", "Q&A", "Cult"];

        // Act
        var result = enrichment.FromEnrichmentHashTagsToHashTags().ToArray();

        // Assert
        result.Should().BeEquivalentTo(
        [
            new RedditPodcastPoster.Subjects.HashTags.HashTag("Alpha Beta", "#AlphaBeta"),
            new RedditPodcastPoster.Subjects.HashTags.HashTag("Cult", "#Cult")
        ]);
    }
}
