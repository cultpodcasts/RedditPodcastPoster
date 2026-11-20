using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// #1484: a split-off episode is hidden until a curator approves it, and until then an ordinary submit never
/// matches (and so never enriches) it in place of the stored episode it was split from.
/// </summary>
public class SubmitSplitOffReviewRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly string _title;
    private readonly DateTime _release;
    private readonly TimeSpan _length;

    public SubmitSplitOffReviewRules()
    {
        _podcast = _fixture.CreatePodcast();
        _title = _fixture.CreateLongTitle();
        _release = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        _length = _fixture.CreateDuration();
        _mocker.Use<IEpisodeHelper>(new EpisodeHelper());
    }

    [Fact(DisplayName =
        "After an overwrite whose stored-episode save failed, a create-new submit of the same episode does not " +
        "enrich the hidden split-off (which would title-match it), and creates a new episode instead.")]
    public async Task create_new_after_partial_overwrite_does_not_enrich_the_split_off()
    {
        // Arrange
        var (stored, splitOff) = StoredWithPartlySavedSplitOff();
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(splitOff);
        var item = SpotifyAndAppleSubmit([splitOff, stored]);
        var sut = ArrangeProcessor();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.CreateNew));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.Episode!.Id.Should().NotBe(splitOff.Id);
        result.Episode.Id.Should().NotBe(stored.Id);
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should().Be(oldSpotifyId);
        EpisodeServicePresence.AppleEpisodeId(splitOff).Should().BeNull();
    }

    [Fact(DisplayName =
        "Once a curator has approved a split-off (cleared removed), it is an ordinary episode again and a submit " +
        "that matches it enriches it.")]
    public async Task approved_split_off_is_matched_normally()
    {
        // Arrange
        var (stored, splitOff) = StoredWithPartlySavedSplitOff();
        splitOff.Removed = false;
        var item = SpotifyAndAppleSubmit([splitOff, stored]);
        var sut = ArrangeProcessor();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.CreateNew));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.Episode!.Id.Should().Be(splitOff.Id);
    }

    [Fact(DisplayName =
        "Re-submitting the Spotify link a hidden split-off holds enriches that split-off, which stays hidden and " +
        "posted: no new public episode is created and nothing is queued for posting.")]
    public async Task resubmitting_split_off_own_link_enriches_it_and_creates_nothing()
    {
        // Arrange
        var (stored, splitOff) = StoredWithCompletedSplitOff();
        var item = SpotifyOnlySubmit([splitOff, stored], EpisodeServicePresence.SpotifyEpisodeId(splitOff)!);
        var sut = ArrangeProcessor();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        result.ServiceLinkConflict.Should().BeNull();
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.Episode!.Id.Should().Be(splitOff.Id);
        splitOff.Removed.Should().BeTrue();
        splitOff.Posted.Should().BeTrue();
        EpisodeServiceLinks.IsAwaitingApproval(splitOff).Should().BeTrue();
        _mocker.GetMock<IEpisodeFactory>().Verify(x => x.CreateEpisode(It.IsAny<CategorisedItem>()), Times.Never);
    }

    [Fact(DisplayName =
        "A submit whose Spotify link conflicts with both the stored episode and its hidden split-off holds against " +
        "the stored episode only: a hidden split-off is never a service-link conflict candidate.")]
    public async Task hidden_split_off_is_not_a_conflict_candidate()
    {
        // Arrange
        var (stored, splitOff) = StoredWithCompletedSplitOff();
        var item = SpotifyOnlySubmit([splitOff, stored], _fixture.CreateSpotifyId());
        var sut = ArrangeProcessor();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        result.ServiceLinkConflict.Should().NotBeNull();
        result.ServiceLinkConflict!.EpisodeId.Should().Be(stored.Id);
        _mocker.GetMock<IEpisodeFactory>().Verify(x => x.CreateEpisode(It.IsAny<CategorisedItem>()), Times.Never);
    }

    /// <summary>
    /// A completed overwrite: the stored episode now carries a new Spotify link, and the hidden split-off holds the
    /// old one.
    /// </summary>
    private (Episode Stored, Episode SplitOff) StoredWithCompletedSplitOff()
    {
        var oldSpotifyId = _fixture.CreateSpotifyId();
        var stored = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithTitle(_title)
            .WithRelease(_release)
            .WithLength(_length)
            .WithSpotify(oldSpotifyId, _fixture.DefaultSpotifyUrl(oldSpotifyId))
            .Create();
        var splitOff = EpisodeServiceLinks.SplitOff(stored, [ServiceKeys.Spotify]).Episode;
        var newSpotifyId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(stored, newSpotifyId);
        EpisodeServicePresence.Upsert(stored, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(newSpotifyId), null);
        return (stored, splitOff);
    }

    private CategorisedItem SpotifyOnlySubmit(IEnumerable<Episode> stored, string spotifyId)
    {
        var spotify = new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(), spotifyId, _podcast.Name, string.Empty, string.Empty, _title,
            string.Empty, _release, _length, _fixture.DefaultSpotifyUrl(spotifyId), false, null);
        return new CategorisedItem(_podcast, stored.ToArray(), null, spotify, null, null, null, Service.Spotify);
    }

    /// <summary>
    /// The stored episode still carries its Spotify and Apple links (its save failed); the split-off saved first
    /// holds the same Spotify link, no Apple link, and is hidden awaiting approval.
    /// </summary>
    private (Episode Stored, Episode SplitOff) StoredWithPartlySavedSplitOff()
    {
        var id = _fixture.CreateGuid();
        var spotifyId = _fixture.CreateSpotifyId();
        var appleId = _fixture.CreateAppleId();
        Episode Stored() => _fixture.BuildEpisode()
            .WithId(id)
            .WithPodcast(_podcast)
            .WithTitle(_title)
            .WithRelease(_release)
            .WithLength(_length)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId))
            .Create();
        var splitOff = EpisodeServiceLinks.SplitOff(Stored(), [ServiceKeys.Spotify]).Episode;
        return (Stored(), splitOff);
    }

    private CategorisedItemProcessor ArrangeProcessor()
    {
        var enricherMocker = new AutoMocker();
        var descriptionHelper = enricherMocker.GetMock<IDescriptionHelper>();
        descriptionHelper
            .Setup(x => x.CollapseDescription(It.IsAny<string?>()))
            .Returns<string?>(description => description ?? string.Empty);
        descriptionHelper
            .Setup(x => x.EnrichMissingDescription(It.IsAny<CategorisedItem>()))
            .Returns(string.Empty);
        enricherMocker.Use(EpisodeDomainTestServices.CreateEnrichmentApplicator());
        enricherMocker.Use(NullLogger<EpisodeEnricher>.Instance);
        _mocker.Use<IEpisodeEnricher>(enricherMocker.CreateInstance<EpisodeEnricher>());
        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(It.IsAny<Podcast>(), It.IsAny<Episode?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));
        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));
        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));
        _mocker.GetMock<IEpisodeFactory>()
            .Setup(x => x.CreateEpisode(It.IsAny<CategorisedItem>()))
            .Returns(() => _fixture.BuildEpisode().WithPodcast(_podcast).WithTitle(_title).Create());
        _mocker.Use<IPodcastProcessor>(_mocker.CreateInstance<PodcastProcessor>());
        _mocker.Use<IServiceLinkConflictGate>(_mocker.CreateInstance<ServiceLinkConflictGate>());
        return _mocker.CreateInstance<CategorisedItemProcessor>();
    }

    private SubmitOptions Options(ServiceLinkConflictChoice choice) =>
        new(_podcast.Id, MatchOtherServices: true, PersistToDatabase: false, ServiceLinkConflict: choice);

    private CategorisedItem SpotifyAndAppleSubmit(IEnumerable<Episode> stored)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var spotify = new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(), spotifyId, _podcast.Name, string.Empty, string.Empty, _title,
            string.Empty, _release, _length, _fixture.DefaultSpotifyUrl(spotifyId), false, null);
        var appleId = _fixture.CreateAppleId();
        var apple = new CategorisedAppleItem(_fixture.CreateAppleId(), appleId, _podcast.Name, string.Empty,
            string.Empty, _title, string.Empty, _release, _length, _fixture.DefaultAppleUrl(appleId), false, null);
        return new CategorisedItem(_podcast, stored.ToArray(), null, spotify, apple, null, null, Service.Spotify);
    }
}
