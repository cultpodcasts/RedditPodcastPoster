using FluentAssertions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// Kind defaults for the podcast flags a TV show / news organisation keeps on <c>indexing</c>: news organisations
/// bypass the short-episode check unless a curator turns it off; TV shows keep the podcast default (off).
/// </summary>
public class CatalogueParentFlagDefaultsRules
{
    [Theory(DisplayName =
        "Catalogue parent flags: a news organisation's effective bypass-short-episodes is the curator value when set " +
        "and on when unset (including no indexing block), because news clips are often short.")]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void news_organisation_bypass_short_defaults_on(bool? stored, bool expected)
    {
        // Arrange
        var organisation = new NewsOrganisation("Synthetic News");
        var indexing = new CatalogueIndexingSource { BypassShortEpisodeChecking = stored };

        // Act
        var effective = CatalogueIndexingSource.EffectiveBypassShortEpisodeChecking(organisation, indexing);
        var transient = indexing.ToTransientPodcast(organisation);

        // Assert
        effective.Should().Be(expected);
        transient.BypassShortEpisodeChecking.Should().Be(expected);
        CatalogueIndexingSource.EffectiveBypassShortEpisodeChecking(organisation, null).Should().BeTrue();
        indexing.BypassShortEpisodeChecking.Should().Be(stored, "the default is read-time only");
    }

    [Theory(DisplayName =
        "Catalogue parent flags: a TV show's effective bypass-short-episodes is exactly the stored value, because only " +
        "news organisations default it on.")]
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
        CatalogueIndexingSource.EffectiveBypassShortEpisodeChecking(show, indexing).Should().Be(stored);
        transient.BypassShortEpisodeChecking.Should().Be(stored);
    }

    [Fact(DisplayName =
        "Catalogue parent flags: always-promote-as-hero survives podcast -> indexing -> transient podcast, because a " +
        "migrated or transferred podcast must keep its hero flag as a TV show or news organisation.")]
    public void always_promote_as_hero_round_trips()
    {
        // Arrange
        var podcast = new Podcast { Name = "Synthetic Podcast", AlwaysPromoteAsHero = true };
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Act
        var indexing = CatalogueIndexingSource.FromPodcast(podcast);
        var transient = indexing.ToTransientPodcast(organisation);

        // Assert
        indexing.AlwaysPromoteAsHero.Should().BeTrue();
        transient.AlwaysPromoteAsHero.Should().BeTrue();
    }
}
