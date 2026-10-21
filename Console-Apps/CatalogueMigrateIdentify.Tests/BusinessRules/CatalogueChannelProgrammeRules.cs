using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Migration;
using Xunit;

namespace CatalogueMigrateIdentify.Tests.BusinessRules;

public class CatalogueChannelProgrammeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Channel programmes: when an iPlayer slug contains series or season, then the programme is the slug prefix, because the channel row is not the show.")]
    public void iplayer_series_slug_is_the_programme()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var programme = _fixture.CreateTitle(2);
        var slug = programme.ToLowerInvariant().Replace(' ', '-')
            + "-series-2-1-"
            + _fixture.CreateTitle(1).ToLowerInvariant().Replace(' ', '-');
        var episode = Stored(podcast, _fixture.CreateTitle(), slug);
        var other = Stored(podcast, _fixture.CreateTitle(), slug: null);

        // Act
        var programmes = CatalogueChannelProgrammes.FromEpisodes([episode, other]);

        // Assert
        programmes.Should().Contain(row => row.Query == programme && row.EpisodeIds.Contains(episode.Id));
        programmes.Should().Contain(row => row.Query == other.Title && row.EpisodeIds.Contains(other.Id));
    }

    [Fact(DisplayName =
        "Channel programmes: when the episode title names the series, then that series is the programme even without a slug, because the series marker is the show.")]
    public void series_marker_in_the_title_is_the_programme()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var programme = _fixture.CreateTitle(2);
        var episode = Stored(podcast, $"{programme} - Series 4: 2. {_fixture.CreateTitle()}", slug: null);

        // Act
        var programmes = CatalogueChannelProgrammes.FromEpisodes([episode]);

        // Assert
        var row = programmes.Should().ContainSingle().Subject;
        row.Query.Should().Be(programme);
        row.EpisodeIds.Should().Equal(episode.Id);
    }

    [Fact(DisplayName =
        "Channel programmes: when episodes are parts of one title, then they are one programme, because a part marker is not a separate show.")]
    public void part_markers_are_one_programme()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var documentary = _fixture.CreateTitle();
        var first = Stored(podcast, $"{documentary} (Part 1)", slug: null);
        var second = Stored(podcast, $"{documentary} (Part 2)", slug: null);

        // Act
        var programmes = CatalogueChannelProgrammes.FromEpisodes([first, second]);

        // Assert
        var row = programmes.Should().ContainSingle().Subject;
        row.Query.Should().Be(documentary);
        row.EpisodeIds.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact(DisplayName =
        "Channel programmes: when two episodes share the clause before a hyphen, then that clause is the programme, and a trailing BBC outlet suffix is not part of the title.")]
    public void shared_brand_drops_the_outlet_suffix()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var brand = _fixture.CreateTitle(2);
        var first = Stored(podcast, $"{brand} - {_fixture.CreateTitle()}", slug: null);
        var second = Stored(podcast, $"{brand} - {_fixture.CreateTitle()} - BBC One", slug: null);

        // Act
        var programmes = CatalogueChannelProgrammes.FromEpisodes([first, second]);

        // Assert
        var row = programmes.Should().ContainSingle().Subject;
        row.Query.Should().Be(brand);
        row.EpisodeIds.Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact(DisplayName =
        "Channel programmes: when one episode title contains a colon, then the whole title is the programme, because a single subtitle is not a shared brand.")]
    public void one_colon_title_stays_whole()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var title = $"{_fixture.CreateTitle(2)}: {_fixture.CreateTitle()}";
        var episode = Stored(podcast, title, slug: null);

        // Act
        var programmes = CatalogueChannelProgrammes.FromEpisodes([episode]);

        // Assert
        var row = programmes.Should().ContainSingle().Subject;
        row.Query.Should().Be(title);
    }

    private Episode Stored(Podcast podcast, string title, string? slug)
    {
        var episode = _fixture.CreateStoredEpisode(podcast, stored => stored.Title = title);
        var id = _fixture.CreateYouTubeId();
        var url = slug is null
            ? new Uri($"https://www.bbc.co.uk/iplayer/episode/{id}")
            : new Uri($"https://www.bbc.co.uk/iplayer/episode/{id}/{slug}");
        EpisodeServicePresence.Upsert(episode, "bbcIplayer", url, image: null);
        return episode;
    }
}
