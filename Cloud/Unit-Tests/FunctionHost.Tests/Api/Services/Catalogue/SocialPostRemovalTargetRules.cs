using System.Globalization;
using FluentAssertions;
using Api.Services.Catalogue;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.SocialPosting.Templates;
using RedditPodcastPoster.UrlShortening.Extensions;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>
/// The curator un-tweet / un-Bluesky target projects the stored item and matches with #1051's
/// SocialPostRemovalFingerprint, so a catalogue post is found by what it actually says and a neighbour's post is not.
/// </summary>
public class SocialPostRemovalTargetRules
{
    private readonly DomainTestFixture _fixture = new();

    private static string Date(DateTime utc) =>
        utc.ToString(SocialPostKindTemplate.ReleaseFormat, CultureInfo.InvariantCulture);

    private static string Length(TimeSpan length) =>
        length.ToString(SocialPostKindTemplate.LengthFormat, CultureInfo.InvariantCulture);

    private TvShowEpisode CreateTvEpisode(string showName, CatalogueRelease release, TimeSpan length)
    {
        var show = new TvShow(showName) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = length };
        episode.SetTvShowProperties(show);
        episode.SetRelease(release);
        return episode;
    }

    private NewsReport CreateReport(string organisationName, CatalogueRelease release, TimeSpan length)
    {
        var organisation = new NewsOrganisation(organisationName) { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = length };
        report.SetNewsOrganisationProperties(organisation);
        report.SetRelease(release);
        return report;
    }

    [Fact(DisplayName =
        "A year-only TV release matches a post whose release line is just the year, because the post never invents a 1 January day.")]
    public void year_only_release_matches_year_line()
    {
        // Arrange
        var parent = _fixture.CreateTitle();
        var release = CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 20);
        var length = _fixture.CreateDuration();
        var target = SocialPostRemovalTarget.For(CreateTvEpisode(parent, release, length));
        var post = $"\"{_fixture.CreateTitle()}\"\n{parent}\n{release.Year} {Length(length)}\n";

        // Act
        var matches = target.Matches(post);

        // Assert
        matches.Should().BeTrue();
        target.Matches(post.Replace(release.Year.ToString(CultureInfo.InvariantCulture),
                (release.Year - 1).ToString(CultureInfo.InvariantCulture)))
            .Should().BeFalse();
    }

    [Fact(DisplayName =
        "A zero-length news report matches a post with no length, and a post with another date does not, because the post leaves an unknown length out.")]
    public void zero_length_omits_length_fragment()
    {
        // Arrange
        var parent = _fixture.CreateTitle();
        var releaseUtc = DomainTestFixture.UtcDateDaysAgo(4);
        var release = CatalogueRelease.FromDate(DateOnly.FromDateTime(releaseUtc));
        var target = SocialPostRemovalTarget.For(CreateReport(parent, release, TimeSpan.Zero));
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
        var target = SocialPostRemovalTarget.For(
            CreateReport(parent, CatalogueRelease.FromDate(DateOnly.FromDateTime(releaseUtc)), length));

        // Act
        var matches = target.Matches($"{parent}\n{Date(releaseUtc)} {Length(length)}");

        // Assert
        matches.Should().BeTrue();
        target.Matches($"{parent}\n{Date(releaseUtc)} {Length(length + TimeSpan.FromMinutes(1))}").Should().BeFalse();
    }

    [Fact(DisplayName =
        "A TV post still matches after the show is renamed, because the site link's short id is the episode's, not the show name.")]
    public void renamed_parent_matches_on_site_link()
    {
        // Arrange
        var release = CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 5);
        var episode = CreateTvEpisode(_fixture.CreateTitle(), release, TimeSpan.Zero);
        var postedLink = CataloguePlayableSiteLink.For(SocialPostRemovalTarget.For(episode).Projection);
        episode.TvShowName = "Renamed " + _fixture.CreateTitle();
        var target = SocialPostRemovalTarget.For(episode);

        // Act
        var matches = target.Matches("\"synthetic\"\nhttps://t.co/synthetic", [postedLink.ToString()]);

        // Assert
        matches.Should().BeTrue();
        target.Matches("\"synthetic\"\nhttps://t.co/synthetic").Should().BeFalse();
    }

    [Fact(DisplayName =
        "A film matches its post by the film's site link, and another film from the same year does not, because film posts have no parent line and the title is re-cased.")]
    public void film_matches_on_site_link()
    {
        // Arrange
        var release = CatalogueRelease.FromYear(DomainTestFixture.UtcToday.Year - 40);
        var film = new Film(_fixture.CreateTitle(4)) { Id = _fixture.CreateGuid(), BlueskyPost = "at://did:plc:synthetic/app.bsky.feed.post/3synthetic" };
        film.SetRelease(release);
        var other = new Film(film.Name) { Id = _fixture.CreateGuid() };
        other.SetRelease(release);
        var target = SocialPostRemovalTarget.For(film);
        var year = release.Year.ToString(CultureInfo.InvariantCulture);
        var link = CataloguePlayableSiteLink.For(target.Projection);
        var otherLink = CataloguePlayableSiteLink.For(SocialPostRemovalTarget.For(other).Projection);

        // Act
        var matches = target.Matches($"\"{film.Name.ToUpperInvariant()}\"\n{year}\n#Film\n{link}");

        // Assert
        matches.Should().BeTrue();
        target.Id.Should().Be(film.Id);
        target.BlueskyPost.Should().Be(film.BlueskyPost);
        target.Matches($"\"{film.Name.ToUpperInvariant()}\"\n{year}\n#Film\n{otherLink}").Should().BeFalse();
    }
}
