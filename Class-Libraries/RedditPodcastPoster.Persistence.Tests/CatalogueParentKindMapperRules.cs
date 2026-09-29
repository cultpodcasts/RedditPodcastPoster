using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueParentKindMapperRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Mapping a podcast to a TV show keeps the podcast id as the TV show id and does not mint a new guid.")]
    public void tv_show_keeps_podcast_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = _fixture.CreateTitle();
            p.TwitterHandle = _fixture.Create<string>();
        });

        // Act
        var show = CatalogueParentKindMapper.ToTvShow(podcast);

        // Assert
        show.Id.Should().Be(podcast.Id);
        show.Name.Should().Be(podcast.Name);
        show.ModelType.Should().Be(ModelType.TvShow);
        show.TwitterHandle.Should().Be(podcast.TwitterHandle);
        show.FileKey.Should().Be(FileKeyFactory.GetTvShowFileKey(podcast.Name));
    }

    [Fact(DisplayName =
        "Mapping a podcast to a news organisation keeps the podcast id as the news organisation id.")]
    public void news_organisation_keeps_podcast_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();

        // Act
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Assert
        organisation.Id.Should().Be(podcast.Id);
        organisation.Name.Should().Be(podcast.Name);
        organisation.ModelType.Should().Be(ModelType.NewsOrganisation);
        organisation.FileKey.Should().Be(FileKeyFactory.GetNewsOrganisationFileKey(podcast.Name));
    }

    [Fact(DisplayName =
        "Mapping an episode to a TV-show episode keeps the episode id and parents it to the TV show id.")]
    public void tv_show_episode_keeps_episode_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var show = CatalogueParentKindMapper.ToTvShow(podcast);

        // Act
        var playable = CatalogueParentKindMapper.ToTvShowEpisode(episode, show);

        // Assert
        playable.Id.Should().Be(episode.Id);
        playable.TvShowId.Should().Be(podcast.Id);
        playable.TvShowName.Should().Be(podcast.Name.Trim());
        playable.Title.Should().Be(episode.Title);
        playable.ModelType.Should().Be(ModelType.TvShowEpisode);
        playable.Services.Should().NotBeNull();
        playable.Services!.ContainsKey(ServiceKeys.YouTube).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Mapping an episode to a news report keeps the episode id and parents it to the news organisation id.")]
    public void news_report_keeps_episode_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Act
        var report = CatalogueParentKindMapper.ToNewsReport(episode, organisation);

        // Assert
        report.Id.Should().Be(episode.Id);
        report.NewsOrganisationId.Should().Be(podcast.Id);
        report.NewsOrganisationName.Should().Be(podcast.Name.Trim());
        report.ModelType.Should().Be(ModelType.NewsReport);
    }

    [Fact(DisplayName =
        "CatalogueParentKind lists only TvShow and NewsOrganisation; Film is not a parent transfer target.")]
    public void parent_kind_enum_excludes_film()
    {
        // Arrange
        var names = Enum.GetNames<CatalogueParentKind>();

        // Act
        var values = Enum.GetValues<CatalogueParentKind>();

        // Assert
        names.Should().BeEquivalentTo([nameof(CatalogueParentKind.TvShow), nameof(CatalogueParentKind.NewsOrganisation)]);
        values.Should().HaveCount(2);
        values.Should().Contain(CatalogueParentKind.TvShow);
        values.Should().Contain(CatalogueParentKind.NewsOrganisation);
        names.Should().NotContain("Film");
    }
}
