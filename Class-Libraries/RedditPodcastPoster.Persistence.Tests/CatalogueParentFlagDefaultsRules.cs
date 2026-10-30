using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Saved defaults for the podcast flags a news organisation keeps on <c>indexing</c>: a news organisation is created
/// (or a podcast is transferred to one) with bypass-short-episodes and always-promote-as-hero stored as true; after
/// that only the stored value counts, so a curator's false sticks. TV shows have no default.
/// </summary>
public class CatalogueParentFlagDefaultsRules
{
    [Theory(DisplayName =
        "Catalogue parent flags: a news organisation indexes with exactly its stored bypass-short and hero flags " +
        "(unset is off), because the news default is saved at creation and never applied on read.")]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void news_organisation_uses_stored_flags_only(bool? stored)
    {
        // Arrange
        var organisation = new NewsOrganisation("Synthetic News");
        var indexing = new CatalogueIndexingSource { BypassShortEpisodeChecking = stored, AlwaysPromoteAsHero = stored };

        // Act
        var transient = indexing.ToTransientPodcast(organisation);

        // Assert
        transient.BypassShortEpisodeChecking.Should().Be(stored);
        transient.AlwaysPromoteAsHero.Should().Be(stored);
        indexing.BypassShortEpisodeChecking.Should().Be(stored);
    }

    [Theory(DisplayName =
        "Catalogue parent flags: a TV show indexes with exactly its stored bypass-short value, because only news " +
        "organisations are created with it on.")]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void tv_show_bypass_short_has_no_default(bool? stored)
    {
        // Arrange
        var show = new TvShow("Synthetic Show");
        var indexing = new CatalogueIndexingSource { BypassShortEpisodeChecking = stored };

        // Act
        var transient = indexing.ToTransientPodcast(show);

        // Assert
        transient.BypassShortEpisodeChecking.Should().Be(stored);
    }

    [Theory(DisplayName =
        "Catalogue parent flags: a podcast transferred to a news organisation (Move to news, CatalogueMigrate) is " +
        "saved with bypass-short and always-promote-as-hero on whatever the podcast had, because those are the news " +
        "defaults.")]
    [InlineData(null, null)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void transfer_to_news_saves_both_flags_on(bool? bypass, bool? hero)
    {
        // Arrange
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(), Name = "Synthetic Podcast", IndexAllEpisodes = true,
            BypassShortEpisodeChecking = bypass, AlwaysPromoteAsHero = hero
        };

        // Act
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Assert
        organisation.Indexing!.BypassShortEpisodeChecking.Should().BeTrue();
        organisation.Indexing.AlwaysPromoteAsHero.Should().BeTrue();
        organisation.Indexing.IndexAllEpisodes.Should().BeTrue("the other discovery settings still come from the podcast");
        podcast.BypassShortEpisodeChecking.Should().Be(bypass, "the source podcast is not changed");
    }

    [Theory(DisplayName =
        "Catalogue parent flags: a podcast transferred to a TV show keeps its own bypass-short and hero flags, because " +
        "the saved news defaults do not apply to TV shows.")]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void transfer_to_tv_keeps_podcast_flags(bool? flag)
    {
        // Arrange
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(), Name = "Synthetic Podcast", BypassShortEpisodeChecking = flag, AlwaysPromoteAsHero = flag
        };

        // Act
        var show = CatalogueParentKindMapper.ToTvShow(podcast);

        // Assert
        show.Indexing!.BypassShortEpisodeChecking.Should().Be(flag);
        show.Indexing.AlwaysPromoteAsHero.Should().Be(flag);
    }

    [Fact(DisplayName =
        "Catalogue parent flags: creation defaults on a news organisation with no indexing block add one holding only " +
        "the two flags, because a URL-submitted news organisation has no discovery settings and must not be " +
        "auto-indexed.")]
    public void creation_defaults_without_indexing_add_only_the_flags()
    {
        // Arrange
        CatalogueIndexingSource? none = null;

        // Act
        var indexing = CatalogueIndexingSource.ApplyNewsOrganisationCreationDefaults(none);

        // Assert
        indexing.BypassShortEpisodeChecking.Should().BeTrue();
        indexing.AlwaysPromoteAsHero.Should().BeTrue();
        indexing.IsAutoIndexed.Should().BeFalse();
        indexing.YouTubeChannelId.Should().BeNull();
        indexing.SpotifyId.Should().BeNull();
        indexing.AppleId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue parent flags: a curator's false saved after creation survives a save and read and is what the " +
        "indexer uses, because the user may override the news defaults after creation.")]
    public void curator_false_after_creation_sticks()
    {
        // Arrange
        var organisation = new NewsOrganisation("Synthetic News") { Id = Guid.NewGuid() };
        organisation.Indexing = CatalogueIndexingSource.ApplyNewsOrganisationCreationDefaults(organisation.Indexing);
        organisation.Indexing.BypassShortEpisodeChecking = false;
        organisation.Indexing.AlwaysPromoteAsHero = false;

        // Act
        var json = JsonSerializer.Serialize(organisation);
        var read = JsonSerializer.Deserialize<NewsOrganisation>(json)!;
        var transient = read.Indexing!.ToTransientPodcast(read);

        // Assert
        read.Indexing.BypassShortEpisodeChecking.Should().BeFalse();
        read.Indexing.AlwaysPromoteAsHero.Should().BeFalse();
        transient.BypassShortEpisodeChecking.Should().BeFalse();
        transient.AlwaysPromoteAsHero.Should().BeFalse();
    }

    [Fact(DisplayName =
        "News defaults backfill patch: with an indexing block only the two flag members are set, conditional on the " +
        "block still existing, because a whole-document or whole-block write would overwrite concurrent edits.")]
    public void backfill_patch_with_indexing_sets_only_the_two_members()
    {
        // Arrange
        const bool indexingStored = true;

        // Act
        var (operations, filter) = NewsOrganisationDefaultsPatchOperations.Build(indexingStored);

        // Assert
        operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Set);
        operations.Select(x => x.Path).Should().BeEquivalentTo(
            "/indexing/bypassShortEpisodeChecking", "/indexing/alwaysPromoteAsHero");
        operations.Cast<PatchOperation<bool>>().Should().OnlyContain(x => x.Value);
        filter.Should().Be("FROM c WHERE IS_OBJECT(c.indexing)");
    }

    [Fact(DisplayName =
        "News defaults backfill patch: with no indexing block one /indexing set holding only the two flags is used, " +
        "conditional on the block still being missing, because Cosmos cannot set a member under a missing parent.")]
    public void backfill_patch_without_indexing_adds_a_block_with_only_the_flags()
    {
        // Arrange
        const bool indexingStored = false;

        // Act
        var (operations, filter) = NewsOrganisationDefaultsPatchOperations.Build(indexingStored);

        // Assert
        var operation = operations.Should().ContainSingle().Subject;
        operation.Path.Should().Be("/indexing");
        var value = ((PatchOperation<JsonNode?>) operation).Value!.AsObject();
        value.Select(x => x.Key).Should().BeEquivalentTo("bypassShortEpisodeChecking", "alwaysPromoteAsHero");
        value["bypassShortEpisodeChecking"]!.GetValue<bool>().Should().BeTrue();
        value["alwaysPromoteAsHero"]!.GetValue<bool>().Should().BeTrue();
        filter.Should().Contain("NOT IS_DEFINED(c.indexing)").And.Contain("IS_NULL(c.indexing)");
    }
}
