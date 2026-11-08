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
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Bluesky.Tests.BusinessRules;

public class BlueskyEmbedCardPostFactorySymbolHashTagRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public BlueskyEmbedCardPostFactorySymbolHashTagRules()
    {
        _mocker.Use<IHashTagEnricher>(new HashTagEnricher());
        _mocker.Use(Options.Create(new BlueskyOptions
        {
            Identifier = "id",
            Password = "pw",
            HashTag = null,
            WithEpisodeUrl = false,
            ReuseSession = false,
            MaxFailures = 1,
            MaxPosts = 1
        }));
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
    }

    [Theory(DisplayName =
        "BlueskyEmbedCardPostFactory: a stored tag that is not permitted on both X and Bluesky is left out of the post, because ToHashTags / IsPermittedHashTag filter those tokens.")]
    [InlineData("Notes on C++ today", "C++")]
    [InlineData("Community LGBTQ+ meetup notes", "LGBTQ+")]
    [InlineData("The Q&A hour starts now", "Q&A")]
    public async Task create_post_leaves_out_a_tag_that_cannot_be_reproduced_as_is(string title, string hashTag)
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.BlueskyHandle = null;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = hashTag;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(podcast.Name);
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
                new PostModel(
                    pe.Item1.Name,
                    string.Empty,
                    string.Empty,
                    [
                        new EpisodePost(
                            pe.Item2.First().Title,
                            pe.Item2.First().Urls.YouTube,
                            pe.Item2.First().Urls.Spotify,
                            pe.Item2.First().Urls.Apple,
                            pe.Item2.First().ReleaseUtc.ToString("o"),
                            pe.Item2.First().Length.ToString(),
                            pe.Item2.First().Description,
                            pe.Item2.First().Id.ToString(),
                            pe.Item2.First().ReleaseUtc,
                            pe.Item2.First().Subjects.ToArray(),
                            pe.Item2.First().Urls.BBC,
                            pe.Item2.First().Urls.InternetArchive)
                    ],
                    null,
                    [],
                    []));
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        post.Text.Should().Contain($"\"{title}\"");
        post.Text.Should().NotContain("#");
    }

    [Fact(DisplayName =
        "BlueskyEmbedCardPostFactory: an enrichment tag stored as Some-Tag is left out of the post, because dash is not permitted in a HashTag on X and Bluesky so the tag is filtered out.")]
    public async Task enrichment_tag_that_shortens_the_stored_text_is_left_out()
    {
        // Arrange
        const string title = "Inside Some-Tag Today";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = ["Some-Tag"];
            p.BlueskyHandle = null;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(podcast.Name);
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
                new PostModel(
                    pe.Item1.Name,
                    string.Empty,
                    string.Empty,
                    [
                        new EpisodePost(
                            pe.Item2.First().Title,
                            pe.Item2.First().Urls.YouTube,
                            pe.Item2.First().Urls.Spotify,
                            pe.Item2.First().Urls.Apple,
                            pe.Item2.First().ReleaseUtc.ToString("o"),
                            pe.Item2.First().Length.ToString(),
                            pe.Item2.First().Description,
                            pe.Item2.First().Id.ToString(),
                            pe.Item2.First().ReleaseUtc,
                            pe.Item2.First().Subjects.ToArray(),
                            pe.Item2.First().Urls.BBC,
                            pe.Item2.First().Urls.InternetArchive)
                    ],
                    null,
                    [],
                    []));
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        post.Text.Should().Contain($"\"{title}\"");
        post.Text.Should().NotContain("#SomeTag");
    }

    [Fact(DisplayName =
        "BlueskyEmbedCardPostFactory: when a title contains a multi-word enrichment phrase, then the posted title uses the compacted MatchingText hashtag, because enrichment tags with spaces gate on MatchingText not the spaced Tag.")]
    public async Task multi_word_enrichment_tag_is_applied_in_title()
    {
        // Arrange
        const string enrichmentPhrase = "Alpha Beta";
        const string title = "Alpha Beta In Focus : How The Pattern Feels";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.HashTag = null;
            p.EnrichmentHashTags = [enrichmentPhrase];
            p.BlueskyHandle = null;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = [];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(podcast.Name);
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
                new PostModel(
                    pe.Item1.Name,
                    string.Empty,
                    string.Empty,
                    [
                        new EpisodePost(
                            pe.Item2.First().Title,
                            pe.Item2.First().Urls.YouTube,
                            pe.Item2.First().Urls.Spotify,
                            pe.Item2.First().Urls.Apple,
                            pe.Item2.First().ReleaseUtc.ToString("o"),
                            pe.Item2.First().Length.ToString(),
                            pe.Item2.First().Description,
                            pe.Item2.First().Id.ToString(),
                            pe.Item2.First().ReleaseUtc,
                            pe.Item2.First().Subjects.ToArray(),
                            pe.Item2.First().Urls.BBC,
                            pe.Item2.First().Urls.InternetArchive)
                    ],
                    null,
                    [],
                    []));
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(new PodcastEpisode(podcast, episode), null);

        // Assert
        post.Text.Should().Contain("\"#AlphaBeta In Focus : How The Pattern Feels\"");
        post.Text.Should().NotContain("Alpha Beta In Focus");
    }
}
