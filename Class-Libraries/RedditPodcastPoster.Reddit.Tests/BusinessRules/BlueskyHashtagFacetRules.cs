using System.Text;
using FluentAssertions;
using idunno.AtProto;
using idunno.Bluesky.RichText;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.RichText;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.Subjects.HashTags;
using HashTag = RedditPodcastPoster.Subjects.HashTags.HashTag;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

public class BlueskyHashtagFacetRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private string? _title;
    private string? _podcastName;
    private List<HashTag> _hashTags = [];

    public BlueskyHashtagFacetRules()
    {
        _mocker.Use<IHashTagEnricher>(new HashTagEnricher());
        _mocker.Use(Options.Create(new BlueskyOptions
        {
            Identifier = _fixture.Create<string>(), Password = _fixture.Create<string>(), HashTag = null,
            WithEpisodeUrl = false, ReuseSession = false, MaxFailures = 1, MaxPosts = 1
        }));
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync(() => _title!);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns(() => _podcastName!);
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(() => _hashTags);
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
                new PostModel(pe.Item1.Name, string.Empty, string.Empty,
                [
                    new EpisodePost(
                        pe.Item2.First().Title, pe.Item2.First().Urls.YouTube, null, null, "1 Jan 2020", "01:00:00",
                        string.Empty, pe.Item2.First().Id.ToString(), pe.Item2.First().ReleaseUtc,
                        pe.Item2.First().Subjects.ToArray(), null, null)
                ],
                null, [], []));
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
    }

    [Fact(DisplayName =
        "Real case (Let's Talk About Sects, 'Opus Dei, Tangara & Redfield'): a subject hashtag inserted at the start of the quoted title gets a tag facet, because the post is wrapped in a quote mark and Bluesky only links the facets we send.")]
    public async Task hashtag_inserted_after_opening_title_quote_is_linked()
    {
        // Arrange
        const string title = "Opus Dei, Tangara & Redfield";
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = "Let's Talk About Sects";
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            p.BlueskyHandle = null;
        });
        var youTubeId = _fixture.CreateYouTubeId();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Subjects = ["Opus Dei", "Catholicism", "Human Trafficking", "Cult"];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        var factory = CreateFactory(podcast, title,
        [
            new HashTag("Opus Dei", "#OpusDei"),
            new HashTag("#Catholicism", null),
            new HashTag("#HumanTrafficking", null),
            new HashTag("#Cult", null)
        ]);
        var post = await factory.Create(new PodcastEpisode(podcast, episode), null);
        var sut = new BlueskyFacetExtractor((_, _) => Task.FromResult<Did?>(null));

        // Act
        var facets = await sut.ExtractFacets(post.Text);

        // Assert
        post.Text.Should().StartWith("\"#OpusDei, Tangara & Redfield\"");
        Tags(facets).Should().BeEquivalentTo("OpusDei", "Catholicism", "HumanTrafficking", "Cult");
        var opusDei = facets.Single(f => f.Features.OfType<TagFacetFeature>().Single().Tag == "OpusDei");
        opusDei.Index.ByteStart.Should().Be(1);
        opusDei.Index.ByteEnd.Should().Be(1 + "#OpusDei".Length);
    }

    [Theory(DisplayName =
        "A subject hashtag inserted into a title word that is followed by a dash, apostrophe or emoji gets a tag facet covering only '#Cult', because Bluesky links exactly the byte range we send and a facet running on to the next word links the wrong tag.")]
    [InlineData("We Left a Hidden Cult—and Spoke Out", "\"We Left a Hidden #Cult—and Spoke Out\"")]
    [InlineData("We Left a Hidden Cult–and Spoke Out", "\"We Left a Hidden #Cult–and Spoke Out\"")]
    [InlineData("Inside a Hidden Cult's Rules", "\"Inside a Hidden #Cult's Rules\"")]
    [InlineData("Café 🙏 Life After a Cult🙏 Today", "\"Café 🙏 Life After a #Cult🙏 Today\"")]
    public async Task inserted_hashtag_facet_stops_at_following_non_word_character(string title, string expectedTitleLine)
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
            e.Subjects = ["Cult"];
            e.HashTag = null;
            e.Guests = null;
            e.Urls = new ServiceUrls { YouTube = new Uri($"https://youtu.be/{youTubeId}") };
        });
        var factory = CreateFactory(podcast, title, [new HashTag("#Cult", null)]);
        var post = await factory.Create(new PodcastEpisode(podcast, episode), null);
        var sut = new BlueskyFacetExtractor((_, _) => Task.FromResult<Did?>(null));

        // Act
        var facets = await sut.ExtractFacets(post.Text);

        // Assert
        post.Text.Should().StartWith(expectedTitleLine);
        var tagFacet = facets.Should().ContainSingle(f => f.Features.OfType<TagFacetFeature>().Any()).Which;
        tagFacet.Features.OfType<TagFacetFeature>().Single().Tag.Should().Be("Cult");
        var bytes = Encoding.UTF8.GetBytes(post.Text);
        var byteStart = Encoding.UTF8.GetByteCount(post.Text.AsSpan(0, post.Text.IndexOf("#Cult", StringComparison.Ordinal)));
        tagFacet.Index.ByteStart.Should().Be(byteStart);
        tagFacet.Index.ByteEnd.Should().Be(byteStart + "#Cult".Length);
        Encoding.UTF8.GetString(bytes, (int)tagFacet.Index.ByteStart,
                (int)(tagFacet.Index.ByteEnd - tagFacet.Index.ByteStart))
            .Should().Be("#Cult");
    }

    private static IEnumerable<string> Tags(IEnumerable<Facet> facets) =>
        facets.SelectMany(f => f.Features).OfType<TagFacetFeature>().Select(t => t.Tag);

    private BlueskyEmbedCardPostFactory CreateFactory(Podcast podcast, string title, List<HashTag> hashTags)
    {
        _title = title;
        _podcastName = podcast.Name;
        _hashTags = hashTags;
        return _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();
    }
}
