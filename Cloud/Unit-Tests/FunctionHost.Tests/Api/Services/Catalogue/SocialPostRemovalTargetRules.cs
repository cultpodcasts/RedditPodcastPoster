using System.Globalization;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>
/// The un-tweet / un-Bluesky text match follows the per-kind post layout from #1051's SocialPostKindTemplate,
/// so a catalogue post is found by what it actually says and a neighbour's post is not.
/// </summary>
public class SocialPostRemovalTargetRules
{
    private readonly DomainTestFixture _fixture = new();

    private static string Date(DateTime utc) =>
        utc.ToString(SocialPostRemovalTarget.ReleaseFormat, CultureInfo.InvariantCulture);

    private static string Length(TimeSpan length) =>
        length.ToString(SocialPostRemovalTarget.LengthFormat, CultureInfo.InvariantCulture);

    [Fact(DisplayName =
        "A year-only TV or news release matches a post whose release line is just the year, because the post never invents a 1 January day.")]
    public void year_only_release_matches_year_line()
    {
        // Arrange
        var parent = _fixture.CreateTitle();
        var release = CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 20);
        var length = _fixture.CreateDuration();
        var target = SocialPostRemovalTarget.ForCatalogueChild(_fixture.CreateGuid(), parent, release,
            release.ToSortUtc(), length, null);
        var post = $"\"{_fixture.CreateTitle()}\"\n{parent}\n{release.Year} {Length(length)}\n";

        // Act
        var matches = target.Matches(post);

        // Assert
        matches.Should().BeTrue();
        target.Matches(post.Replace(release.Year.ToString(CultureInfo.InvariantCulture), (release.Year - 1).ToString(CultureInfo.InvariantCulture)))
            .Should().BeFalse();
    }

    [Fact(DisplayName =
        "A zero-length TV or news item matches a post with no length, and a post with another date does not, because the post leaves an unknown length out.")]
    public void zero_length_omits_length_fragment()
    {
        // Arrange
        var parent = _fixture.CreateTitle();
        var releaseUtc = DomainTestFixture.UtcDateDaysAgo(4);
        var release = CatalogueRelease.FromDate(DateOnly.FromDateTime(releaseUtc));
        var target = SocialPostRemovalTarget.ForCatalogueChild(_fixture.CreateGuid(), parent, release, releaseUtc,
            TimeSpan.Zero, null);
        var title = _fixture.CreateTitle();

        // Act
        var matches = target.Matches($"\"{title}\"\n{parent}\n{Date(releaseUtc)}\n");

        // Assert
        matches.Should().BeTrue();
        target.Matches($"\"{title}\"\n{parent}\n{Date(releaseUtc.AddDays(-1))}\n").Should().BeFalse();
    }

    [Fact(DisplayName =
        "A news report with a known length needs the length in the post, because two reports from one outlet on one day differ by length.")]
    public void known_length_is_required()
    {
        // Arrange
        var parent = _fixture.CreateTitle();
        var releaseUtc = DomainTestFixture.UtcDateDaysAgo(2);
        var length = _fixture.CreateDuration();
        var target = SocialPostRemovalTarget.ForCatalogueChild(_fixture.CreateGuid(), parent,
            CatalogueRelease.FromDate(DateOnly.FromDateTime(releaseUtc)), releaseUtc, length, null);

        // Act
        var matches = target.Matches($"{parent}\n{Date(releaseUtc)} {Length(length)}");

        // Assert
        matches.Should().BeTrue();
        target.Matches($"{parent}\n{Date(releaseUtc)} {Length(length + TimeSpan.FromMinutes(1))}").Should().BeFalse();
    }

    [Fact(DisplayName =
        "A film matches its post by the re-cased opening of its quoted title with no parent line, and another film from the same year does not, because film posts have no parent line.")]
    public void film_matches_on_title_prefix_without_parent()
    {
        // Arrange
        var name = _fixture.CreateTitle(4);
        var other = "Zz" + _fixture.CreateTitle(4);
        var release = CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 40);
        var target = SocialPostRemovalTarget.ForFilm(_fixture.CreateGuid(), name, release, release.ToSortUtc(),
            TimeSpan.Zero, null);
        var year = release.Year.ToString(CultureInfo.InvariantCulture);

        // Act
        var matches = target.Matches($"\"{name.ToUpperInvariant()}\"\n{year}\n#Film\n");

        // Assert
        matches.Should().BeTrue();
        target.Matches($"\"{other}\"\n{year}\n#Film\n").Should().BeFalse();
    }
}
