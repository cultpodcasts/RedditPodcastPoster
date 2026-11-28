using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// RPP #1412: a TV show that holds a URL only as a trailer is the catalogue owner of that URL, so a re-submit of the
/// trailer's URL stops as already-exists and never re-creates the episode that was attached as the trailer.
/// </summary>
public class TvShowTrailerUrlOwnershipRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public TvShowTrailerUrlOwnershipRules()
    {
        _mocker.Use(Options.Create(new SubmitContentTypesOptions()));
        _mocker.Use<IEpisodeRepository>(new InMemoryEpisodeRepository());
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create(
            _mocker.GetMock<IBBCPageMetaDataExtractor>().Object,
            archiveExtractor: _mocker.GetMock<IInternetArchivePageMetaDataExtractor>().Object));
    }

    private TvShow ShowHoldingYouTubeTrailer(string youTubeId)
    {
        var show = new TvShow(_fixture.CreateTitle())
        {
            Trailers =
            [
                new TvShowTrailer
                {
                    Id = _fixture.CreateGuid(),
                    Title = _fixture.CreateTitle(),
                    Url = new Uri($"https://www.youtube.com/watch?v={youTubeId}"),
                    Ids = new EpisodeIds { YouTube = youTubeId }
                }
            ]
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShow, bool>> predicate) =>
                predicate.Compile()(show) ? show : null);
        return show;
    }

    [Fact(DisplayName =
        "When a YouTube URL is held by a TV show only as a trailer, the catalogue owner is that show: kind TvShowEpisode, the show's name and id as parent, no playable id, and a trailer flag, because the source episode was deleted on attach.")]
    public async Task youtube_url_held_as_a_tv_show_trailer_is_owned_by_the_show()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var show = ShowHoldingYouTubeTrailer(youTubeId);
        var pasted = new Uri($"https://youtu.be/{youTubeId}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(pasted, CancellationToken.None);

        // Assert
        owner.Should().NotBeNull();
        owner!.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        owner.ParentName.Should().Be(show.Name);
        owner.ParentId.Should().Be(show.Id);
        owner.PlayableId.Should().BeNull();
        owner.IsTrailer.Should().BeTrue();
        owner.AlwaysStopsSubmit.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a Spotify URL is held by a TV show only through a trailer's stored Spotify id, the catalogue owner is that show, because a YouTube-sourced trailer keeps its Spotify id so a Spotify re-find still matches.")]
    public async Task spotify_url_held_through_a_trailer_spotify_id_is_owned_by_the_show()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var url = new Uri($"https://open.spotify.com/episode/{spotifyId}");
        var show = new TvShow(_fixture.CreateTitle())
        {
            Trailers =
            [
                new TvShowTrailer
                {
                    Id = _fixture.CreateGuid(),
                    Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
                    Ids = new EpisodeIds { Spotify = spotifyId }
                }
            ]
        };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShow, bool>> predicate) =>
                predicate.Compile()(show) ? show : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(url, CancellationToken.None);

        // Assert
        owner.Should().NotBeNull();
        owner!.ParentId.Should().Be(show.Id);
        owner.IsTrailer.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a YouTube id is held by a TV show trailer, the indexer's holder check names that show unless the show is in the excluded ids, because a show's own trailer must not count against the show it is being indexed into.")]
    public async Task youtube_holder_check_finds_a_tv_show_trailer_unless_excluded()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var show = ShowHoldingYouTubeTrailer(youTubeId);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var holder = await sut.FindYouTubeHolderExcluding(youTubeId, [], CancellationToken.None);
        var excluded = await sut.FindYouTubeHolderExcluding(youTubeId, [show.Id], CancellationToken.None);

        // Assert
        holder.Should().NotBeNull();
        holder!.ParentId.Should().Be(show.Id);
        holder.IsTrailer.Should().BeTrue();
        excluded.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV show trailer owner blocks a submit even when the submit is routed to that same TV show, because a trailer has no resubmit path and the show's parent submitter would create a second TV episode.")]
    public void tv_show_trailer_owner_blocks_a_submit_routed_to_its_own_show()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle());
        var trailerOwner = new CatalogueServiceUrlOwner(
            SubmitClassification.TvShowEpisode, show.Name, PlayableId: null, ParentId: show.Id, IsTrailer: true);
        var episodeOwner = new CatalogueServiceUrlOwner(
            SubmitClassification.TvShowEpisode, show.Name, _fixture.CreateGuid(), show.Id);
        var parent = CatalogueParentMatch.For(show);

        // Act
        var trailerBlocks = CatalogueOwnershipGuard.Blocks(trailerOwner, parent);
        var episodeBlocks = CatalogueOwnershipGuard.Blocks(episodeOwner, parent);

        // Assert
        trailerBlocks.Should().BeTrue();
        episodeBlocks.Should().BeFalse();
    }
}
