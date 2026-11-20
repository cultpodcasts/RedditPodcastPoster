using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Curation;
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
        _mocker.Use<IEpisodeHelper>(_mocker.CreateInstance<EpisodeHelper>());
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

    [Theory(DisplayName =
        "When an overwrite's stored-episode save throws after the split-off was saved, and the curator retries the " +
        "overwrite, the stored episode is overwritten and the saved split-off is reused, so there is one split-off " +
        "holding the old links, never a second copy or a split of the split-off (#1482).")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task overwrite_retried_after_stored_save_failure_is_idempotent(bool splitOffListedFirst)
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var item = SpotifySubmit(_title, [stored]);
        var episodes = new InMemoryEpisodeRepository();
        episodes.Seed(stored);
        var saves = 0;
        episodes.OnSaving = () =>
        {
            if (++saves == 2)
            {
                throw new InvalidOperationException(_fixture.CreateTitle());
            }
        };
        _mocker.Use<IEpisodeRepository>(episodes);
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        UseRealPodcastPipeline();
        var options = Options(ServiceLinkConflictChoice.Overwrite, stored.Id);
        var failed = () => _mocker.CreateInstance<CategorisedItemProcessor>().ProcessCategorisedItem(item, options);
        await failed.Should().ThrowAsync<InvalidOperationException>();
        var firstSplitOff = episodes.SavedEpisodes.Should().ContainSingle().Subject;
        episodes.OnSaving = null;
        var reloadedStored = (await episodes.GetEpisode(_podcast.Id, stored.Id))!;
        var reloadedSplitOff = (await episodes.GetEpisode(_podcast.Id, firstSplitOff.Id))!;
        Episode[] reloaded = splitOffListedFirst ? [reloadedSplitOff, reloadedStored] : [reloadedStored, reloadedSplitOff];
        var retry = item with { MatchingPodcastEpisodes = reloaded };

        // Act
        var result = await _mocker.CreateInstance<CategorisedItemProcessor>().ProcessCategorisedItem(retry, options);

        // Assert
        result.SplitOffEpisode!.Id.Should().Be(firstSplitOff.Id);
        var podcastEpisodes = await episodes.GetByPodcastId(_podcast.Id).ToListAsync();
        podcastEpisodes.Should().HaveCount(2);
        var saved = podcastEpisodes.Single(x => x.Id == stored.Id);
        EpisodeServicePresence.SpotifyEpisodeId(saved).Should().Be(item.ResolvedSpotifyItem!.EpisodeId);
        var splitOff = podcastEpisodes.Single(x => x.Id == firstSplitOff.Id);
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should().Be(oldSpotifyId);
    }

    [Theory(DisplayName =
        "When a curator re-submits after an overwrite whose stored-episode save failed, the prompt holds the stored " +
        "episode, never the split-off that holds the same links, whatever the list order (#1482).")]
    [InlineData(false)]
    [InlineData(true)]
    public void prompt_after_partial_overwrite_holds_the_stored_episode(bool splitOffListedFirst)
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [stored]);
        var storedCopy = StoredEpisodeWithSpotify(_title);
        storedCopy.Id = stored.Id;
        EpisodeServicePresence.SetSpotifyIdentity(storedCopy, EpisodeServicePresence.SpotifyEpisodeId(stored));
        EpisodeServicePresence.Upsert(storedCopy, ServiceKeys.Spotify,
            EpisodeServicePresence.TryGetUrl(stored, ServiceKeys.Spotify), null);
        var splitOff = EpisodeServiceLinks.SplitOff(storedCopy, [ServiceKeys.Spotify]).Episode;
        Episode[] reloaded = splitOffListedFirst ? [splitOff, stored] : [stored, splitOff];

        // Act
        var outcome = Gate.Evaluate(item with { MatchingPodcastEpisodes = reloaded },
            Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Hold);
        outcome.Hold!.EpisodeId.Should().Be(stored.Id);
        VerifyDropLogged(splitOff.Id, Times.Once());
    }

    [Theory(DisplayName =
        "When an overwrite's secondary Apple link resolves on one attempt and not on the retry (or the other way " +
        "round), the retry still finds the split-off saved by the failed attempt, so there is one split-off holding " +
        "both old links and the stored episode keeps neither (#1482).")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task overwrite_retry_with_apple_resolving_differently_reuses_the_split_off(bool appleOnFirstAttempt)
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var oldAppleId = _fixture.CreateAppleId();
        EpisodeServicePresence.SetAppleIdentity(stored, oldAppleId);
        EpisodeServicePresence.Upsert(stored, ServiceKeys.Apple, _fixture.DefaultAppleUrl(oldAppleId), null);
        var spotifyOnly = SpotifySubmit(_title, [stored]);
        var withApple = spotifyOnly with { ResolvedAppleItem = AppleItem(_title) };
        var episodes = new InMemoryEpisodeRepository();
        episodes.Seed(stored);
        var saves = 0;
        episodes.OnSaving = () =>
        {
            if (++saves == 2)
            {
                throw new InvalidOperationException(_fixture.CreateTitle());
            }
        };
        _mocker.Use<IEpisodeRepository>(episodes);
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        UseRealPodcastPipeline();
        var options = Options(ServiceLinkConflictChoice.Overwrite, stored.Id);
        var first = appleOnFirstAttempt ? withApple : spotifyOnly;
        var failed = () => _mocker.CreateInstance<CategorisedItemProcessor>().ProcessCategorisedItem(first, options);
        await failed.Should().ThrowAsync<InvalidOperationException>();
        var firstSplitOff = episodes.SavedEpisodes.Should().ContainSingle().Subject;
        episodes.OnSaving = null;
        Episode[] reloaded =
        [
            (await episodes.GetEpisode(_podcast.Id, stored.Id))!,
            (await episodes.GetEpisode(_podcast.Id, firstSplitOff.Id))!
        ];
        var retry = (appleOnFirstAttempt ? spotifyOnly : withApple) with { MatchingPodcastEpisodes = reloaded };

        // Act
        var result = await _mocker.CreateInstance<CategorisedItemProcessor>().ProcessCategorisedItem(retry, options);

        // Assert
        result.SplitOffEpisode!.Id.Should().Be(firstSplitOff.Id);
        var podcastEpisodes = await episodes.GetByPodcastId(_podcast.Id).ToListAsync();
        podcastEpisodes.Should().HaveCount(2);
        var splitOff = podcastEpisodes.Single(x => x.Id == firstSplitOff.Id);
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should().Be(oldSpotifyId);
        EpisodeServicePresence.AppleEpisodeId(splitOff).Should().Be(oldAppleId);
        var saved = podcastEpisodes.Single(x => x.Id == stored.Id);
        EpisodeServicePresence.SpotifyEpisodeId(saved).Should().Be(retry.ResolvedSpotifyItem!.EpisodeId);
        EpisodeServicePresence.AppleEpisodeId(saved).Should().NotBe(oldAppleId);
    }

    [Fact(DisplayName =
        "When an overwrite is retried after its stored-episode save failed and a curator has since edited the " +
        "split-off, the retry reuses the split-off without saving over it, so the curator's edits are kept (#1482).")]
    public async Task overwrite_retry_keeps_curator_edits_to_the_split_off()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var oldSpotifyId = EpisodeServicePresence.SpotifyEpisodeId(stored);
        var item = SpotifySubmit(_title, [stored]);
        var episodes = new InMemoryEpisodeRepository();
        episodes.Seed(stored);
        var saves = 0;
        episodes.OnSaving = () =>
        {
            if (++saves == 2)
            {
                throw new InvalidOperationException(_fixture.CreateTitle());
            }
        };
        _mocker.Use<IEpisodeRepository>(episodes);
        _mocker.Use<IPodcastRepository>(new InMemoryPodcastRepository());
        UseRealPodcastPipeline();
        var options = Options(ServiceLinkConflictChoice.Overwrite, stored.Id);
        var failed = () => _mocker.CreateInstance<CategorisedItemProcessor>().ProcessCategorisedItem(item, options);
        await failed.Should().ThrowAsync<InvalidOperationException>();
        var firstSplitOff = episodes.SavedEpisodes.Should().ContainSingle().Subject;
        episodes.OnSaving = null;
        var edited = (await episodes.GetEpisode(_podcast.Id, firstSplitOff.Id))!;
        var curatorTitle = _fixture.CreateTitle();
        edited.Title = curatorTitle;
        episodes.Seed(edited);
        Episode[] reloaded = [(await episodes.GetEpisode(_podcast.Id, stored.Id))!, edited];
        var retry = item with { MatchingPodcastEpisodes = reloaded };

        // Act
        var result = await _mocker.CreateInstance<CategorisedItemProcessor>().ProcessCategorisedItem(retry, options);

        // Assert
        result.SplitOffEpisode!.Id.Should().Be(firstSplitOff.Id);
        episodes.SavedEpisodes.Count(x => x.Id == firstSplitOff.Id).Should().Be(1, "only the failed attempt saved it");
        var splitOff = (await episodes.GetEpisode(_podcast.Id, firstSplitOff.Id))!;
        splitOff.Title.Should().Be(curatorTitle);
        EpisodeServicePresence.SpotifyEpisodeId(splitOff).Should().Be(oldSpotifyId);
        var saved = (await episodes.GetEpisode(_podcast.Id, stored.Id))!;
        EpisodeServicePresence.SpotifyEpisodeId(saved).Should().Be(item.ResolvedSpotifyItem!.EpisodeId);
    }

    [Fact(DisplayName =
        "An approved split-off whose stored episode has since been overwritten (it now carries different links) is " +
        "a normal conflict candidate again: it is not dropped and can be held for the curator (#1482, #1484).")]
    public void split_off_of_an_overwritten_episode_stays_a_candidate()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var splitOff = EpisodeServiceLinks.SplitOff(stored, [ServiceKeys.Spotify]).Episode;
        // #1484: a pending split-off is never a candidate; once approved it is an ordinary episode.
        EpisodeServiceLinks.Approve(splitOff);
        var newSpotifyId = _fixture.CreateSpotifyId();
        EpisodeServicePresence.SetSpotifyIdentity(stored, newSpotifyId);
        EpisodeServicePresence.Upsert(stored, ServiceKeys.Spotify, _fixture.DefaultSpotifyUrl(newSpotifyId), null);
        stored.Title = $"{_title} {_fixture.CreateShortTitle()}";
        var item = SpotifySubmit(_title, [splitOff, stored]);
        Helper.ConflictingServiceKeys(stored, item).Should().NotBeEmpty("the stored episode is a candidate too");

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Hold);
        outcome.Hold!.EpisodeId.Should().Be(splitOff.Id);
        VerifyDropLogged(splitOff.Id, Times.Never());
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

    private void VerifyDropLogged(Guid splitOffId, Times times) =>
        _mocker.GetMock<ILogger<ServiceLinkConflictGate>>().Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains(splitOffId.ToString()) &&
                    state.ToString()!.Contains("split-off of a partly saved overwrite")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

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

    [Fact(DisplayName =
        "When a stored episode has the same title but was released months before the submitted Spotify episode, it " +
        "is not a service-link conflict and the curator is not asked, because a recurring title (Bonus, Trailer, " +
        "Part 1) is a different episode and an overwrite would move its links onto the wrong one (#1481).")]
    public void same_title_months_apart_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title, release: _release.AddMonths(-4));
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        keys.Should().BeEmpty();
        outcome.Should().Be(ServiceLinkConflictOutcome.Proceed);
    }

    [Fact(DisplayName =
        "When a stored episode has the same title and release day but a very different length, it is not a " +
        "service-link conflict, because the #1445 cross-platform duration band must also agree (#1481).")]
    public void same_title_and_day_with_a_different_length_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title, length: _length + TimeSpan.FromMinutes(30));
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the submitted Spotify episode has no known length, a same-title stored episode is not a service-link " +
        "conflict, because without duration evidence the title alone cannot justify moving links (#1481).")]
    public void submit_without_a_length_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title);
        var item = SpotifySubmit(_title, [stored], length: TimeSpan.Zero);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the stored episode was released two days apart from the submit (another calendar day, outside the " +
        "same-release window), it is not a service-link conflict even with the same title and length (#1481).")]
    public void same_title_on_another_day_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title, release: _release.AddDays(-2));
        var item = SpotifySubmit(_title, [stored]);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "When two stored episodes with identical titles both conflict, the hold names the one released nearer the " +
        "submit, whatever order the episodes are listed in, so list order never picks the episode to overwrite (#1481).")]
    [InlineData(false)]
    [InlineData(true)]
    public void identical_title_tie_picks_the_nearest_release(bool nearerListedFirst)
    {
        // Arrange
        var nearer = StoredEpisodeWithSpotify(_title, release: _release.AddMinutes(5));
        var further = StoredEpisodeWithSpotify(_title, release: _release.AddHours(-2));
        Episode[] stored = nearerListedFirst ? [nearer, further] : [further, nearer];
        var item = SpotifySubmit(_title, stored);

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        Helper.ConflictingServiceKeys(further, item).Should().Equal(ServiceKeys.Spotify);
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Hold);
        outcome.Hold!.EpisodeId.Should().Be(nearer.Id);
    }

    [Theory(DisplayName =
        "When two stored episodes with identical titles and releases both conflict, the hold names the one nearer the " +
        "submitted length, whatever the list order (#1481).")]
    [InlineData(false)]
    [InlineData(true)]
    public void identical_title_and_release_tie_picks_the_nearest_length(bool nearerListedFirst)
    {
        // Arrange
        var nearer = StoredEpisodeWithSpotify(_title, length: _length + TimeSpan.FromSeconds(1));
        var further = StoredEpisodeWithSpotify(_title, length: _length + TimeSpan.FromSeconds(90));
        Episode[] stored = nearerListedFirst ? [nearer, further] : [further, nearer];
        var item = SpotifySubmit(_title, stored);

        // Act
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        outcome.Hold!.EpisodeId.Should().Be(nearer.Id);
    }

    [Fact(DisplayName =
        "When two conflicting stored episodes are identical in title, release and length, the hold names the same " +
        "episode in either list order (the lower id), so a re-prompt after a reload holds the same episode (#1481).")]
    public void full_tie_is_stable_across_list_order()
    {
        // Arrange
        var first = StoredEpisodeWithSpotify(_title);
        var second = StoredEpisodeWithSpotify(_title);
        var expected = first.Id.CompareTo(second.Id) < 0 ? first.Id : second.Id;

        // Act
        var forward = Gate.Evaluate(SpotifySubmit(_title, [first, second]), Options(ServiceLinkConflictChoice.Prompt));
        var reversed = Gate.Evaluate(SpotifySubmit(_title, [second, first]), Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        forward.Hold!.EpisodeId.Should().Be(expected);
        reversed.Hold!.EpisodeId.Should().Be(expected);
    }

    /// <summary>The Front's YouTube publishing delay (-1:07:00:00): YouTube goes out 31 hours before the audio.</summary>
    private static readonly TimeSpan TheFrontDelay = -(TimeSpan.FromDays(1) + TimeSpan.FromHours(7));

    private static readonly TimeSpan AudioLength = TimeSpan.FromMinutes(60);

    [Fact(DisplayName =
        "When an audio-only stored episode with the same title was released one YouTube publishing delay before the " +
        "submit, it is not a service-link conflict, because delay alignment only applies when the stored release can " +
        "be a YouTube release; for audio vs audio it would pick the previous day's recurring episode (#1481).")]
    public void audio_only_stored_episode_a_delay_apart_is_not_a_conflict()
    {
        // Arrange
        GivenYouTubePublishingDelay(TheFrontDelay);
        var submitRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12));
        var stored = StoredEpisodeWithSpotify(_title, release: submitRelease + TheFrontDelay, length: AudioLength);
        var item = SpotifySubmit(_title, [stored], AudioLength, submitRelease);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a stored episode carrying YouTube with the same title was published one YouTube publishing delay before " +
        "the submitted audio, it is a service-link conflict, because its release is the YouTube release (#1445 rules).")]
    public void youtube_bearing_stored_episode_a_delay_apart_is_a_conflict()
    {
        // Arrange
        GivenYouTubePublishingDelay(TheFrontDelay);
        var submitRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12));
        var stored = StoredYouTubeEpisodeWithSpotify(_title, submitRelease + TheFrontDelay, AudioLength);
        var item = SpotifySubmit(_title, [stored], AudioLength, submitRelease);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().Equal(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "The Front regression (#1416): a YouTube-first stored episode holding the wrong Spotify id, its YouTube cut " +
        "10% shorter than the audio and published a negative publishing delay earlier, is a service-link conflict and " +
        "a curator submit is held for it, instead of silently creating a duplicate.")]
    public void the_front_youtube_first_episode_with_wrong_spotify_is_held()
    {
        // Arrange
        GivenYouTubePublishingDelay(TheFrontDelay);
        var submitRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(11));
        var youTubeRelease = submitRelease + TheFrontDelay + TimeSpan.FromMinutes(12);
        var stored = StoredYouTubeEpisodeWithSpotify(_title, youTubeRelease, AudioLength * 0.9);
        var item = SpotifySubmit(_title, [stored], AudioLength, submitRelease);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);
        var outcome = Gate.Evaluate(item, Options(ServiceLinkConflictChoice.Prompt));

        // Assert
        keys.Should().Equal(ServiceKeys.Spotify);
        outcome.Kind.Should().Be(ServiceLinkConflictOutcomeKind.Hold);
        outcome.Hold!.EpisodeId.Should().Be(stored.Id);
    }

    [Theory(DisplayName =
        "When a same-title stored episode was released on the same calendar day as the submit but more than three " +
        "hours apart, it is a service-link conflict whether it carries YouTube or is audio-only, because Spotify " +
        "releases are date-only and YouTube and audio often go out hours apart on the same day.")]
    [InlineData(false)]
    [InlineData(true)]
    public void same_calendar_day_hours_apart_is_a_conflict(bool storedCarriesYouTube)
    {
        // Arrange
        var submitRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(20));
        var storedRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(1));
        var stored = storedCarriesYouTube
            ? StoredYouTubeEpisodeWithSpotify(_title, storedRelease, AudioLength)
            : StoredEpisodeWithSpotify(_title, release: storedRelease, length: AudioLength);
        var item = SpotifySubmit(_title, [stored], AudioLength, submitRelease);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().Equal(ServiceKeys.Spotify);
    }

    [Fact(DisplayName =
        "When an audio-only stored episode is 10% shorter than the submitted audio, it is not a service-link conflict, " +
        "because audio vs audio is the same file and keeps the strict band (greater of 2 min or 3%).")]
    public void audio_only_stored_episode_10_percent_shorter_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title, length: AudioLength * 0.9);
        var item = SpotifySubmit(_title, [stored], AudioLength);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a stored episode carrying YouTube is 20% shorter than the submitted audio, it is not a service-link " +
        "conflict, because that is outside the title-supported YouTube↔audio band (greater of 5 min or 12%).")]
    public void youtube_bearing_stored_episode_20_percent_shorter_is_not_a_conflict()
    {
        // Arrange
        var stored = StoredYouTubeEpisodeWithSpotify(_title, _release, AudioLength * 0.8);
        var item = SpotifySubmit(_title, [stored], AudioLength);

        // Act
        var keys = Helper.ConflictingServiceKeys(stored, item);

        // Assert
        keys.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a title-matching candidate is rejected on release or length, an Information log names the episode, so " +
        "a duplicate created instead of a conflict prompt can be explained from the logs.")]
    public void rejected_title_match_is_logged()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_title, release: _release.AddMonths(-4));
        var item = SpotifySubmit(_title, [stored]);

        // Act
        Helper.ConflictingServiceKeys(stored, item);

        // Assert
        _mocker.GetMock<ILogger<EpisodeHelper>>().Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains(stored.Id.ToString())),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When the stored episode's title does not match the submit, nothing is logged, because only a title-matching " +
        "candidate rejected on release or length needs explaining.")]
    public void unrelated_title_is_not_logged()
    {
        // Arrange
        var stored = StoredEpisodeWithSpotify(_fixture.CreateTitleNotContainedIn(_title));
        var item = SpotifySubmit(_title, [stored]);

        // Act
        Helper.ConflictingServiceKeys(stored, item);

        // Assert
        _mocker.GetMock<ILogger<EpisodeHelper>>().Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private SubmitOptions Options(ServiceLinkConflictChoice choice, Guid? episodeId = null) =>
        new(_podcast.Id, MatchOtherServices: true, ServiceLinkConflict: choice, ServiceLinkConflictEpisodeId: episodeId);

    private Episode StoredEpisodeWithSpotify(string title, DateTime? release = null, TimeSpan? length = null)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        return _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithTitle(title)
            .WithRelease(release ?? _release)
            .WithLength(length ?? _length)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
    }

    /// <summary>A YouTube-first stored episode that also carries a (wrong) Spotify id: release and length are YouTube's.</summary>
    private Episode StoredYouTubeEpisodeWithSpotify(string title, DateTime release, TimeSpan length)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var youTubeId = _fixture.CreateYouTubeId();
        return _fixture.BuildEpisode()
            .WithPodcast(_podcast)
            .WithTitle(title)
            .WithRelease(release)
            .WithLength(length)
            .WithYouTube(youTubeId, _fixture.DefaultYouTubeUrl(youTubeId))
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId))
            .Create();
    }

    private void GivenYouTubePublishingDelay(TimeSpan delay) => _podcast.YouTubePublicationOffset = delay.Ticks;

    private CategorisedItem SpotifySubmit(string title, IEnumerable<Episode> stored, TimeSpan? length = null,
        DateTime? release = null)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var spotify = new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(), spotifyId, _podcast.Name, string.Empty, string.Empty, title,
            string.Empty, release ?? _release, length ?? _length, _fixture.DefaultSpotifyUrl(spotifyId), false, null);
        return new CategorisedItem(_podcast, stored.ToArray(), null, spotify, null, null, null, Service.Spotify);
    }

    private CategorisedAppleItem AppleItem(string title)
    {
        var appleId = _fixture.CreateAppleId();
        return new CategorisedAppleItem(_fixture.CreateAppleId(), appleId, _podcast.Name, string.Empty, string.Empty,
            title, string.Empty, _release, _length, _fixture.DefaultAppleUrl(appleId), false, null);
    }
}
