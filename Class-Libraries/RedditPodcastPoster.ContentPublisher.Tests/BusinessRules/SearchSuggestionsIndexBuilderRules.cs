using FluentAssertions;
using RedditPodcastPoster.ContentPublisher.Builders;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class SearchSuggestionsIndexBuilderRules
{
    [Fact(DisplayName =
        "Search suggestions index: when a subject has a name and aliases, then the flat index includes a primary-name row and one row per alias with searchText lowercased, because typeahead matches pre-normalized text.")]
    public void subject_name_and_aliases_emit_flat_rows()
    {
        // Arrange
        var subject = new Subject("Primary Topic") { Aliases = ["PT", "Primary Alias"] };
        var generatedAt = DateTime.UtcNow;

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([subject], [], generatedAt);

        // Assert
        corpus.GeneratedAtUtc.Should().Be(generatedAt);
        corpus.Entries.Should().BeEquivalentTo(
        [
            new { Type = "subject", Canonical = "Primary Topic", SearchText = "primary alias", Alias = (string?)"Primary Alias" },
            new { Type = "subject", Canonical = "Primary Topic", SearchText = "primary topic", Alias = (string?)null },
            new { Type = "subject", Canonical = "Primary Topic", SearchText = "pt", Alias = (string?)"PT" }
        ], options => options.WithStrictOrdering());
    }

    [Fact(DisplayName =
        "Search suggestions index: when a subject has associatedSubjects, then those names are not indexed, because associated subjects are excluded from typeahead.")]
    public void associated_subjects_are_excluded()
    {
        // Arrange
        var subject = new Subject("Primary Topic")
        {
            Aliases = ["PT"],
            AssociatedSubjects = ["Related Topic"]
        };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([subject], []);

        // Assert
        corpus.Entries.Should().NotContain(e =>
            e.SearchText == "related topic" || e.Canonical == "Related Topic");
        corpus.Entries.Should().ContainSingle(e => e.SearchText == "primary topic");
        corpus.Entries.Should().ContainSingle(e => e.SearchText == "pt");
    }

    [Fact(DisplayName =
        "Search suggestions index: when a podcast is not removed, then its name is indexed as a podcast row with lowercased searchText.")]
    public void active_podcast_name_is_indexed()
    {
        // Arrange
        var podcast = new Podcast { Name = "Example Show" };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [podcast]);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Type = "podcast",
                Canonical = "Example Show",
                SearchText = "example show",
                Alias = (string?)null
            });
    }

    [Fact(DisplayName =
        "Search suggestions index: when a podcast is marked removed, then it is omitted from the index, because removed shows must not appear in typeahead.")]
    public void removed_podcast_is_omitted()
    {
        // Arrange
        var active = new Podcast { Name = "Active Show" };
        var removed = new Podcast { Name = "Retired Show", Removed = true };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [active, removed]);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Canonical.Should().Be("Active Show");
    }

    [Fact(DisplayName =
        "Search suggestions index: when duplicate type+canonical+searchText rows would be emitted, then only one row is kept, because the match key must be unique.")]
    public void duplicate_match_keys_are_deduped()
    {
        // Arrange
        var subject = new Subject("Primary Topic") { Aliases = ["Primary Topic", " primary topic "] };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([subject], []);

        // Assert
        corpus.Entries.Should().ContainSingle(e => e.Type == "subject" && e.SearchText == "primary topic");
    }
}
