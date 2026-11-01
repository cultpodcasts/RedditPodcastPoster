using System.Globalization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>Bluesky post length budget (#1097): the quoted title line must fit inside 300 graphemes.</summary>
public class BlueskyEmbedCardPostFactoryLengthRules
{
    private const int BlueskyGraphemeLimit = 300;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private string? _title;
    private string? _podcastName;

    public BlueskyEmbedCardPostFactoryLengthRules()
    {
        _mocker.Use(Options.Create(new BlueskyOptions
        {
            Identifier = _fixture.Create<string>(),
            Password = _fixture.Create<string>(),
            HashTag = null,
            WithEpisodeUrl = false,
            ReuseSession = false,
            MaxFailures = 1,
            MaxPosts = 1
        }));
        _mocker.GetMock<ITextSanitiser>()
            .Setup(sanitiser => sanitiser.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(() => _title!);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(sanitiser => sanitiser.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(() => _podcastName!);
        _mocker.GetMock<IHashTagProvider>()
            .Setup(provider => provider.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(resolver => resolver.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact(DisplayName =
        "When a title is too long for one Bluesky post, the truncated post including its quote marks and line break is at most 300 graphemes, because Bluesky rejects longer posts.")]
    public async Task truncated_post_fits_the_bluesky_grapheme_limit()
    {
        // Arrange
        var (podcast, episode) = CreatePodcastEpisode();
        _title = LongerThanOnePost(_fixture.CreateTitle());
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        QuotedTitle(post.Text).Should().EndWith("…");
        GraphemeCount(post.Text).Should().BeLessThanOrEqualTo(BlueskyGraphemeLimit);
    }

    [Fact(DisplayName =
        "When a title exactly fills the 300-character budget, the post keeps the whole title and is 300 characters, because the budget only cuts titles that would overflow.")]
    public async Task title_that_exactly_fits_is_not_truncated()
    {
        // Arrange
        var (podcast, episode) = CreatePodcastEpisode();
        var overhead = await PostOverhead(podcast, episode);
        _title = TitleOfLength(BlueskyGraphemeLimit - overhead);
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        QuotedTitle(post.Text).Should().Be(_title);
        post.Text.Length.Should().Be(BlueskyGraphemeLimit);
    }

    [Fact(DisplayName =
        "When a title is one character over the 300-character budget, the title is cut with an ellipsis and the post stays within 300 characters, because the quote marks and line break are budgeted.")]
    public async Task title_one_over_the_budget_is_truncated_within_the_limit()
    {
        // Arrange
        var (podcast, episode) = CreatePodcastEpisode();
        var overhead = await PostOverhead(podcast, episode);
        _title = TitleOfLength(BlueskyGraphemeLimit - overhead + 1);
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        QuotedTitle(post.Text).Should().EndWith("…");
        post.Text.Length.Should().BeLessThanOrEqualTo(BlueskyGraphemeLimit);
    }

    [Fact(DisplayName =
        "When the cut point of a long title falls inside an emoji, the whole emoji is dropped rather than split, because half a surrogate pair is not valid text.")]
    public async Task truncation_never_splits_a_surrogate_pair()
    {
        // Arrange
        var (podcast, episode) = CreatePodcastEpisode();
        // Truncation assertion depends on the exact characters: every emoji here is a UTF-16 surrogate pair.
        _title = string.Concat(Enumerable.Repeat("\U0001F600", BlueskyGraphemeLimit));
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        var title = QuotedTitle(post.Text);
        title.Should().EndWith("…");
        title.TrimEnd('…').Should().Be(string.Concat(Enumerable.Repeat("\U0001F600", (title.Length - 1) / 2)));
        post.Text.Any(char.IsSurrogate).Should().BeTrue();
        post.Text.Where((c, i) => char.IsHighSurrogate(c) && (i + 1 >= post.Text.Length || !char.IsLowSurrogate(post.Text[i + 1])))
            .Should().BeEmpty();
        GraphemeCount(post.Text).Should().BeLessThanOrEqualTo(BlueskyGraphemeLimit);
    }

    private (RedditPodcastPoster.Models.Podcasts.Podcast Podcast, Episode Episode) CreatePodcastEpisode()
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.BlueskyHandle = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Subjects = [];
        episode.HashTag = null;
        _podcastName = podcast.Name;
        return (podcast, episode);
    }

    /// <summary>Post length minus title length, measured with a short title that is never truncated.</summary>
    private async Task<int> PostOverhead(RedditPodcastPoster.Models.Podcasts.Podcast podcast, Episode episode)
    {
        _title = TitleOfLength(20);
        var post = await _mocker.CreateInstance<BlueskyEmbedCardPostFactory>()
            .Create(new PodcastEpisode(podcast, episode), null);
        return post.Text.Length - _title.Length;
    }

    private string TitleOfLength(int length)
    {
        var words = _fixture.CreateTitle();
        while (words.Length < length)
        {
            words += " " + _fixture.CreateTitle();
        }

        return words[..length].TrimEnd().PadRight(length, 'x');
    }

    private string LongerThanOnePost(string seed)
    {
        var title = seed;
        while (title.Length <= BlueskyGraphemeLimit)
        {
            title += " " + _fixture.CreateTitle();
        }

        return title;
    }

    private static string QuotedTitle(string post)
    {
        var firstLine = post.Split(Environment.NewLine)[0];
        firstLine.Should().StartWith("\"").And.EndWith("\"");
        return firstLine[1..^1];
    }

    private static int GraphemeCount(string text) => new StringInfo(text).LengthInTextElements;
}
