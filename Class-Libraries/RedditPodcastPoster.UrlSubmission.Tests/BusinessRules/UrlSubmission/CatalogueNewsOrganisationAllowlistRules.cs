using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CatalogueNewsOrganisationAllowlistRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "S-008 is the frozen NewsOrganisation apply allowlist of 317 publisher names, " +
        "because News migrate apply must not use the Identify name heuristic alone.")]
    public void frozen_count_matches_the_name_set()
    {
        // Arrange
        // Act
        var names = CatalogueNewsOrganisationAllowlist.Names;

        // Assert
        names.Count.Should().Be(CatalogueNewsOrganisationAllowlist.FrozenCount);
        names.Count.Should().Be(317);
    }

    [Fact(DisplayName =
        "S-008 membership is case-insensitive and trims the publisher name, " +
        "because Cosmos names may differ in casing or surrounding whitespace.")]
    public void contains_is_case_insensitive_and_trims()
    {
        // Arrange
        var stored = CatalogueNewsOrganisationAllowlist.Names.First();

        // Act
        var exact = CatalogueNewsOrganisationAllowlist.Contains(stored);
        var folded = CatalogueNewsOrganisationAllowlist.Contains(stored.ToUpperInvariant());
        var padded = CatalogueNewsOrganisationAllowlist.Contains($"  {stored}  ");
        var missing = CatalogueNewsOrganisationAllowlist.Contains(null);
        var blank = CatalogueNewsOrganisationAllowlist.Contains("   ");

        // Assert
        exact.Should().BeTrue();
        folded.Should().BeTrue();
        padded.Should().BeTrue();
        missing.Should().BeFalse();
        blank.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Stay-podcast publisher names are not on S-008, " +
        "because partisan, advocacy, and news-satire rows remain podcasts.")]
    public void stay_podcast_names_are_not_allowlisted()
    {
        // Arrange
        var stayNames = CatalogueMigrateIdentify.StayPodcastPublisherNames;

        // Act
        var allowlistedStayNames = stayNames
            .Where(CatalogueNewsOrganisationAllowlist.Contains)
            .ToList();

        // Assert
        stayNames.Should().NotBeEmpty();
        allowlistedStayNames.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A YouTube-only four-letter publisher can be a dry-run News candidate without being on S-008, " +
        "because the name heuristic discovers stations and the allowlist is only for apply.")]
    public void four_letter_heuristic_is_not_apply_approval()
    {
        // Arrange
        var callSign = CreateFourLetterNameOffAllowlist();
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = callSign;
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.SpotifyId = string.Empty;
            p.AppleId = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var result = CatalogueMigrateIdentify.FromPodcast(podcast, [episode]);
        var onAllowlist = CatalogueNewsOrganisationAllowlist.Contains(callSign);

        // Assert
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.RequiresAllowlist.Should().BeTrue();
        onAllowlist.Should().BeFalse();
    }

    private string CreateFourLetterNameOffAllowlist()
    {
        var seed = Math.Abs(_fixture.Create<int>());
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var name = string.Create(4, seed + attempt, static (span, value) =>
            {
                var n = value;
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[Math.Abs(n) % 26];
                    n = HashCode.Combine(n, i);
                }
            });
            if (!CatalogueTvShowCanonicalNames.IsPublisherName(name)
                && !CatalogueNewsOrganisationAllowlist.Contains(name))
            {
                return name;
            }
        }

        return "QZXJ";
    }
}
