using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// #1416: a submitted Spotify / Apple URL that matches a stored episode except that the episode already carries a
/// different id for that service. The curator chooses overwrite (the stored links split off into a new episode)
/// or create a new episode; callers without a curator keep the create-new behaviour.
/// </summary>
public class SubmitServiceLinkConflictRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;
    private readonly string _title;
    private readonly DateTime _release;
    private readonly TimeSpan _length;

    public SubmitServiceLinkConflictRules()
    {
        _podcast = _fixture.CreatePodcast();
        _title = _fixture.CreateLongTitle();
        _release = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        _length = _fixture.CreateDuration();
        _mocker.Use<IEpisodeHelper>(new EpisodeHelper());
    }

    private IEpisodeHelper Helper => _mocker.Get<IEpisodeHelper>();

    private ServiceLinkConflictGate Gate => _mocker.CreateInstance<ServiceLinkConflictGate>();

    [Fact(DisplayName =
        "When a Spotify URL is submitted and a stored episode with the same title already carries a different " +
        "Spotify id, the Spotify link is a service-link conflict.")]
    public void same_title_with_different_spotify_id_is_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        Helper.IsMatchingEpisode(stored, item).Should().BeFalse("a conflicting identity never matches silently");
        keys.Should().Equal(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "When a Spotify submit with a resolved Apple episode conflicts with a stored episode on both ids, both the " +
        "Spotify and Apple links are in conflict.")]
    public void spotify_and_apple_both_conflict()
    {
        // Arrange
        var appleId = _fixture.CreateAppleId();
        var stored = StoredEpisodeWithSpotify(_title);
        EpisodeServicePresence.SetAppleIdentity(stored, appleId);
        EpisodeServicePresence.Upsert(stored, ServiceKeys.Apple, _fixture.DefaultAppleUrl(appleId), null);
        var item = SpotifySubmit(_title, [stored]) with { ResolvedAppleItem = AppleItem(_title) };

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().Equal(ServiceKeys.Spotify, ServiceKeys.Apple);
    }

    [Fact(DisplayName =
        "When the stored episode with a different Spotify id has an unrelated title, it is not a conflict, because " +
        "the conflict prompt only fires for an episode the submit would otherwise attach to.")]
    public void unrelated_title_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_fixture.CreateTitleNotContainedIn(_title));
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a YouTube URL is submitted, a different stored Spotify id is not a service-link conflict, because the " +
        "curator choice covers Spotify and Apple submits only.")]
    public void youtube_submit_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var youTubeId = _fixture.CreateYouTubeId();
        var youTube = new CategorisedYouTubeItem(
            _fixture.CreateYouTubeChannelId(), youTubeId, _podcast.Name, string.Empty, string.Empty, _title,
            string.Empty, _release, _length, _fixture.DefaultYouTubeUrl(youTubeId), false, null, null);
        var item = new CategorisedItem(
            _podcast, [stored], null, SpotifySubmit(_title, [stored]).ResolvedSpotifyItem, null, youTube, null,
            Service.YouTube);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a curator submit (prompt) hits a service-link conflict, nothing is changed and the hold names the " +
        "stored episode with its stored and the submitted Spotify links.")]
    public void prompt_holds_with_stored_and_submitted_links()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var storedUrl = EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify);
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Hold);
        outcome.Hold!.EpisodeId.Should().Be(stored.Id);
        outcome.Hold.EpisodeTitle.Should().Be(_title);
        outcome.Hold.Links.Should().ContainSingle().Which.Should().Be(
            new ServiceLinkConflictLink(ServiceKeys.Spotify, storedUrl, item.ResolvedSpotifyItem!.Url));
        EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify).Should().Be(storedUrl);
    }

    [Fact(DisplayName =
        "When the caller does not ask for the curator choice (create-new, the default), a service-link conflict " +
        "proceeds to create a new episode as before.")]
    public void create_new_proceeds()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var outcome = Gate.Evaluate(item, new SubmitOptions(_podcast.Id, MatchOtherServices: true));

        // Assert
        outcome.Should().Be(ServiceLinkConflictOutcome.Proceed);
    }

    [Fact(DisplayName =
        "When the curator answers overwrite for the held episode, its old Spotify link is split off into a new " +
        "episode of the same podcast and the stored episode is cleared to take the submitted link.")]
    public void overwrite_splits_old_links_off()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Overwrite, stored.Id));

        // Assert
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Overwritten);
        outcome.Episode.Should().BeSameAs(stored);
        EpisodeServicePresence.SpotifyEpisodeId(stored).Should().BeNull();
        outcome.SplitOff!.PodcastId.Should().Be(stored.PodcastId);
        EpisodeServicePresence.SpotifyEpisodeId(outcome.SplitOff).Should().Be(oldSpotifyId);
    }

    [Fact(DisplayName =
        "When an overwrite answer names a different episode from the one in conflict, nothing is changed and the " +
        "curator is asked again, so a stale answer never overwrites another episode.")]
    public void overwrite_for_another_episode_holds_again()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Overwrite, _fixture.CreateGuid()));

        // Assert
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Hold);
        outcome.Hold!.EpisodeId.Should().Be(stored.Id);
        EpisodeServicePresence.SpotifyEpisodeId(stored).Should().Be(oldSpotifyId);
    }

    [Fact(DisplayName = "When the curator answers cancel, nothing is written and the stored episode keeps its links.")]
    public void cancel_writes_nothing()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Cancel));

        // Assert
        outcome.Should().Be(ServiceLinkConflictOutcome.Cancelled);
        EpisodeServicePresence.SpotifyEpisodeId(stored).Should().Be(oldSpotifyId);
    }

    [Fact(DisplayName =
        "When another stored episode already matches the submit, there is no conflict prompt, because the submit " +
        "enriches that episode as before.")]
    public void existing_match_proceeds_without_prompt()
    {
        // Arrange
        var conflicting = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [conflicting]);
        var matching = _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithTitle(_fixture.CreateTitleNotContainedIn(_title))
            .WithSpotify(item.ResolvedSpotifyItem!.EpisodeId)
            .Create();
        item = item with { MatchingPodcastEpisodes = [conflicting, matching] };

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        outcome.Should().Be(ServiceLinkConflictOutcome.Proceed);
    }

    [Fact(DisplayName =
        "When the curator overwrites, the split-off episode is saved before the stored episode, and both are " +
        "returned, so the old links are never lost if the second save fails.")]
    public async Task overwrite_saves_split_off_then_stored_episode()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [stored]);
        var splitOff = _fixture.CreateStoredEpisodeWithSpotifyOnly(_podcast);
        var episodes = new InMemoryEpisodeRepository();
        _mocker.Use<IEpisodeRepository>(episodes);
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        _mocker.GetMock<IServiceLinkConflictGate>()
            .Setup(x => x.Evaluate(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .Returns(ServiceLinkConflictOutcome.Overwritten(stored, splitOff));
        _mocker.GetMock<IPodcastProcessor>()
            .Setup(x => x.AddEpisodeToExistingPodcast(It.Is<CategorisedItem>(i => i.MatchingEpisode == stored)))
            .ReturnsAsync(new SubmitResult(SubmitResultState.None, SubmitResultState.None, Episode: stored));
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.Overwrite, stored.Id));

        // Assert
        episodes.SavedEpisodes.Select(x => x.Id).Should().Equal(splitOff.Id, stored.Id);
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.SplitOffEpisode.Should().BeSameAs(splitOff);
    }

    [Fact(DisplayName =
        "When the curator overwrites and the real enrich path runs, the stored episode carries the submitted " +
        "Spotify id and url, the split-off carries the old ones, and the split-off is saved first.")]
    public async Task overwrite_end_to_end_moves_old_link_and_applies_submitted_link()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var oldSpotifyUrl = EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify);
        var item = SpotifySubmit(_title, [stored]);
        var episodes = new InMemoryEpisodeRepository();
        _mocker.Use<IEpisodeRepository>(episodes);
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        UseRealPodcastPipeline();
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.Overwrite, stored.Id));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.Episode.Should().BeSameAs(stored);
        EpisodeServicePresence.SpotifyEpisodeId(stored).Should().Be(item.ResolvedSpotifyItem!.EpisodeId);
        EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify).Should().Be(item.ResolvedSpotifyItem.Url);
        var splitOff = result.SplitOffEpisode!;
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should().Be(oldSpotifyId);
        EpisodeServicePresence.TryGetUrl(splitOff, ServiceKeys.Spotify).Should().Be(oldSpotifyUrl);
        episodes.SavedEpisodes.Select(x => x.Id).Should().Equal(splitOff.Id, stored.Id);
    }

    [Fact(DisplayName =
        "When a service-link conflict is held for the curator, nothing is saved and the episode is neither " +
        "enriched nor created.")]
    public async Task hold_saves_nothing()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [stored]);
        var hold = new ServiceLinkConflictHold(stored.Id, stored.Title, stored.ReleaseUtc, []);
        var episodes = new InMemoryEpisodeRepository();
        _mocker.Use<IEpisodeRepository>(episodes);
        _mocker.GetMock<IServiceLinkConflictGate>()
            .Setup(x => x.Evaluate(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .Returns(ServiceLinkConflictOutcome.Held(hold));
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        result.ServiceLinkConflict.Should().Be(hold);
        episodes.SavedEpisodes.Should().BeEmpty();
        _mocker.GetMock<IPodcastProcessor>().Verify(
            x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()), Times.Never);
    }

    [Theory(DisplayName = "A curator's service-link conflict answer parses from its wire value; absent means prompt.")]
    [InlineData(null, ServiceLinkConflictChoice.Prompt)]
    [InlineData("overwrite", ServiceLinkConflictChoice.Overwrite)]
    [InlineData("Create-New", ServiceLinkConflictChoice.CreateNew)]
    [InlineData(" cancel ", ServiceLinkConflictChoice.Cancel)]
    public void answer_parses(string? wire, ServiceLinkConflictChoice expected)
    {
        // Arrange
        var value = wire;

        // Act
        var parsed = ServiceLinkConflictChoices.TryParseAnswer(value, out var choice);

        // Assert
        parsed.Should().BeTrue();
        choice.Should().Be(expected);
    }

    [Fact(DisplayName = "An unknown service-link conflict answer is rejected rather than read as a prompt.")]
    public void unknown_answer_is_rejected()
    {
        // Arrange
        var value = _fixture.CreateTitle();

        // Act
        var parsed = ServiceLinkConflictChoices.TryParseAnswer(value, out _);

        // Assert
        parsed.Should().BeFalse();
    }

    /// <summary>Real gate, matcher, enricher and podcast processor; only subject/guest lookups are stubbed.</summary>
    private void UseRealPodcastPipeline()
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
        _mocker.Use<IPodcastProcessor>(_mocker.CreateInstance<PodcastProcessor>());
        _mocker.Use<IServiceLinkConflictGate>(Gate);
    }

    private SubmitOptions Options(ServiceLinkConflictChoice choice, Guid? episodeId = null) =>
        new(_podcast.Id, MatchOtherServices: true, ServiceLinkConflict: choice, ServiceLinkConflictEpisodeId: episodeId);

    private Episode StoredEpisodeWithSpotify(string title)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        return _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithTitle(title)
            .WithRelease(_release)
            .WithLength(_length)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
    }

    private CategorisedItem SpotifySubmit(string title, IEnumerable<Episode> stored)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var spotify = new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(), spotifyId, _podcast.Name, string.Empty, string.Empty, title,
            string.Empty, _release, _length, _fixture.DefaultSpotifyUrl(spotifyId), false, null);
        return new CategorisedItem(_podcast, stored.ToArray(), null, spotify, null, null, null, Service.Spotify);
    }

    private CategorisedAppleItem AppleItem(string title)
    {
        var appleId = _fixture.CreateAppleId();
        return new CategorisedAppleItem(_fixture.CreateAppleId(), appleId, _podcast.Name, string.Empty, string.Empty,
            title, string.Empty, _release, _length, _fixture.DefaultAppleUrl(appleId), false, null);
    }
}
