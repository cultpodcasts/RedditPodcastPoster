using FluentAssertions;
using idunno.AtProto;
using idunno.Bluesky.RichText;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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

    private static IEnumerable<string> Tags(IEnumerable<Facet> facets) =>
        facets.SelectMany(f => f.Features).OfType<TagFacetFeature>().Select(t => t.Tag);

    private static BlueskyEmbedCardPostFactory CreateFactory(Podcast podcast, string title, List<HashTag> hashTags)
    {
        var textSanitiser = new Mock<ITextSanitiser>();
        textSanitiser.Setup(x => x.SanitiseTitle(It.IsAny<PostModel>())).ReturnsAsync(title);
        textSanitiser.Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>())).Returns(podcast.Name);
        var hashTagProvider = new Mock<IHashTagProvider>();
        hashTagProvider.Setup(x => x.GetHashTags(It.IsAny<List<string>>())).ReturnsAsync(hashTags);
        var postModelFactory = new Mock<IPostModelFactory>();
        postModelFactory
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns((ValueTuple<Podcast, IEnumerable<Episode>> pe, bool _) =>
                new PostModel(pe.Item1.Name, string.Empty, string.Empty,
                [
                    new EpisodePost(
                        title, pe.Item2.First().Urls.YouTube, null, null, "1 Jan 2020", "01:00:00",
                        string.Empty, pe.Item2.First().Id.ToString(), pe.Item2.First().ReleaseUtc,
                        pe.Item2.First().Subjects.ToArray(), null, null)
                ],
                null, [], []));
        var guestResolver = new Mock<IPersonGuestHandleResolver>();
        guestResolver.Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
        var options = Options.Create(new BlueskyOptions
        {
            Identifier = "id", Password = "pw", HashTag = null, WithEpisodeUrl = false,
            ReuseSession = false, MaxFailures = 1, MaxPosts = 1
        });
        return new BlueskyEmbedCardPostFactory(
            textSanitiser.Object,
            new HashTagEnricher(),
            hashTagProvider.Object,
            postModelFactory.Object,
            guestResolver.Object,
            options,
            Mock.Of<ILogger<IBlueskyEmbedCardPostFactory>>());
    }
}
