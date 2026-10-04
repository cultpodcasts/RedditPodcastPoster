using System.Net;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class NonPodcastEpisodeMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private IEpisodeHelper Sut => _mocker.CreateInstance<EpisodeHelper>();

    [Fact(DisplayName =
        "When a BBC Sounds URL is submitted against a podcast whose Spotify episode has the same title, " +
        "the stored episode matches so Sounds can be attached instead of creating a duplicate.")]
    public void bbc_sounds_submit_matches_existing_spotify_episode_by_title()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: title);
        var categorisedItem = OtherSubmit(podcast, [episode], title, StreamingService.BbcSounds, SoundsPlayUrl());

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When an Internet Archive URL is submitted against a podcast whose Spotify episode has the same title, " +
        "the stored episode matches so the archive link can be attached instead of creating a duplicate.")]
    public void internet_archive_submit_matches_existing_spotify_episode_by_title()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: title);
        var categorisedItem = OtherSubmit(
            podcast, [episode], title, StreamingService.InternetArchive, InternetArchiveUrl());

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a BBC Sounds URL is submitted against a podcast episode with a different title and no Sounds URL, " +
        "the stored episode does not match.")]
    public void bbc_sounds_submit_does_not_match_unrelated_title()
    {
        // Arrange
        var (storedTitle, submittedTitle) = DistinctTitles();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: storedTitle);
        var categorisedItem = OtherSubmit(
            podcast, [episode], submittedTitle, StreamingService.BbcSounds, SoundsPlayUrl());

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the stored episode already has the submitted BBC Sounds URL, it matches even if titles differ.")]
    public void bbc_sounds_submit_matches_existing_sounds_url()
    {
        // Arrange
        var (storedTitle, submittedTitle) = DistinctTitles();
        var soundsUrl = SoundsPlayUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: storedTitle);
        EpisodeServicePresence.Upsert(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds), soundsUrl, null);
        var categorisedItem = OtherSubmit(
            podcast, [episode], submittedTitle, StreamingService.BbcSounds, soundsUrl);

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the stored episode already has a different BBC Sounds URL and the submitted Sounds title matches, " +
        "the episode does not match because EpisodeEnricher would no-op on HasUrl.")]
    public void bbc_sounds_submit_does_not_title_match_when_sounds_url_conflicts()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var storedUrl = SoundsPlayUrl();
        var submittedUrl = SoundsPlayUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: title);
        EpisodeServicePresence.Upsert(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds), storedUrl, null);
        var categorisedItem = OtherSubmit(
            podcast, [episode], title, StreamingService.BbcSounds, submittedUrl);

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a stored Spotify URL equals the submitted Sounds play URL but titles differ, " +
        "the episode does not match because URL compare is keyed to BbcSounds.")]
    public void bbc_sounds_submit_does_not_match_spotify_url_with_same_absolute_uri()
    {
        // Arrange
        var (storedTitle, submittedTitle) = DistinctTitles();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: storedTitle);
        var submittedUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        var categorisedItem = OtherSubmit(
            podcast, [episode], submittedTitle, StreamingService.BbcSounds, submittedUrl);

        // Act
        var result = Sut.IsMatchingEpisode(episode, categorisedItem);

        // Assert
        result.Should().BeFalse();
    }

    private (string Stored, string Submitted) DistinctTitles()
    {
        string stored;
        string submitted;
        do
        {
            stored = _fixture.CreateTitle();
            submitted = _fixture.CreateTitle();
        } while (TitlesCouldSubstringMatch(stored, submitted));

        return (stored, submitted);
    }

    private static bool TitlesCouldSubstringMatch(string stored, string submitted)
    {
        var episodeTitle = WebUtility.HtmlDecode(stored.Trim());
        var resolvedTitle = WebUtility.HtmlDecode(submitted.Trim());
        return resolvedTitle == episodeTitle ||
               resolvedTitle.Contains(episodeTitle) ||
               episodeTitle.Contains(resolvedTitle);
    }

    private CategorisedItem OtherSubmit(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        string title,
        StreamingService streamingService,
        Uri url)
    {
        return new CategorisedItem(
            podcast,
            episodes,
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                streamingService,
                podcast,
                null,
                url,
                title),
            Service.Other);
    }

    private Uri SoundsPlayUrl()
    {
        var playId = _fixture.CreateGuid().ToString("N");
        return new Uri($"https://www.bbc.co.uk/sounds/play/{playId}");
    }

    private Uri InternetArchiveUrl() =>
        new($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
}
