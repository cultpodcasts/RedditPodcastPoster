using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Bluesky.Client;
using RedditPodcastPoster.Bluesky.Configuration;
using RedditPodcastPoster.Bluesky.Factories;
using RedditPodcastPoster.Bluesky.Managers;
using RedditPodcastPoster.Bluesky.Models;
using RedditPodcastPoster.Bluesky.Posters;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Posting;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.People.Resolvers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.SocialPosting.Episodes;
using RedditPodcastPoster.SocialPosting.Factories;
using RedditPodcastPoster.Subjects.HashTags;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Twitter.Builders;
using RedditPodcastPoster.Twitter.Clients;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Models;
using RedditPodcastPoster.Twitter.Posters;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Services;

namespace RedditPodcastPoster.Reddit.Tests.BusinessRules;

/// <summary>
/// Twitter and Bluesky post templates, links and posted-state writes for TV episodes, news reports and films.
/// </summary>
public class CatalogueSocialPostRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public CatalogueSocialPostRules()
    {
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<PostModel>()))
            .ReturnsAsync((PostModel pm) => pm.EpisodeTitle);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitisePodcastName(It.IsAny<PostModel>()))
            .Returns((PostModel pm) => pm.PodcastName);
        _mocker.GetMock<IHashTagEnricher>()
            .Setup(x => x.AddHashTag(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
            .Returns((string input, string _, string? _) => (input, false));
        _mocker.GetMock<IHashTagProvider>()
            .Setup(x => x.GetHashTags(It.IsAny<List<string>>()))
            .ReturnsAsync(new List<HashTag>());
        _mocker.GetMock<IPostModelFactory>()
            .Setup(x => x.ToPostModel(It.IsAny<(Podcast, IEnumerable<Episode>)>(), It.IsAny<bool>()))
            .Returns(((Podcast Podcast, IEnumerable<Episode> Episodes) pe, bool _) => new PostModel(
                pe.Podcast.Name, string.Empty, string.Empty,
                pe.Episodes.Select(e => new EpisodePost(
                    e.Title, e.Urls.YouTube, e.Urls.Spotify, e.Urls.Apple,
                    e.ReleaseUtc.ToString("d MMM yyyy"), e.Length.ToString(@"\[h\:mm\:ss\]"),
                    e.Description, e.Id.ToString(), e.ReleaseUtc, e.Subjects.ToArray(),
                    e.Urls.BBC, e.Urls.InternetArchive)),
                null, [], []));
        _mocker.GetMock<IPersonGuestHandleResolver>()
            .Setup(x => x.Resolve(It.IsAny<Episode>()))
            .ReturnsAsync((Array.Empty<string>(), Array.Empty<string>()));
        _mocker.Use(Options.Create(new TwitterOptions
        {
            ConsumerKey = "k", ConsumerSecret = "s", AccessToken = "t", AccessTokenSecret = "ts",
            WithEpisodeUrl = true, ShortUrlOnlyWhenShareImage = false
        }));
        _mocker.Use(Options.Create(new BlueskyOptions
        {
            Identifier = "id", Password = "pw", WithEpisodeUrl = true, ShortUrlOnlyWhenShareImage = false,
            ReuseSession = false, MaxFailures = 3, MaxPosts = 2
        }));
        _mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = TimeSpan.FromMinutes(1), TweetDays = 2, RedditDays = 2, BlueSkyDays = 2,
            CategoriserDays = 2
        }));
    }

    // ---- fixtures -------------------------------------------------------------------------

    private (TvShow Show, TvShowEpisode Episode) CreateTvEpisode(Action<Podcast>? customise = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = "Synthetic Cult Docs";
            p.TwitterHandle = "@synthdocs";
            p.BlueskyHandle = "@synthdocs.example";
            p.HashTag = null;
            p.EnrichmentHashTags = null;
            customise?.Invoke(p);
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast,
            release: DomainTestFixture.UtcDaysAgo(1).AddHours(9), length: TimeSpan.FromMinutes(42),
            title: "Inside the synthetic commune");
        episode.Subjects = [];
        episode.Guests = null;
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        return (show, CatalogueParentKindMapper.ToTvShowEpisode(episode, show));
    }

    private (NewsOrganisation Organisation, NewsReport Report) CreateNewsReport()
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = "Synthetic Evening News";
            p.TwitterHandle = string.Empty;
            p.BlueskyHandle = "@synthnews.example";
            p.HashTag = null;
            p.EnrichmentHashTags = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast,
            release: DomainTestFixture.UtcDaysAgo(1).AddHours(18), length: TimeSpan.FromMinutes(6),
            title: "Report on a synthetic cult raid");
        episode.Subjects = [];
        episode.Guests = null;
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        return (organisation, CatalogueParentKindMapper.ToNewsReport(episode, organisation));
    }

    private Film CreateFilm(CatalogueRelease? release = null, TimeSpan? length = null)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = "Synthetic Film Uploads";
            p.TwitterHandle = "@synthfilms";
            p.BlueskyHandle = null;
            p.HashTag = null;
            p.EnrichmentHashTags = null;
        });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast,
            release: DomainTestFixture.UtcDaysAgo(1).AddHours(12), length: length ?? TimeSpan.FromMinutes(95),
            title: "The Synthetic Prophet");
        episode.Subjects = [];
        episode.Guests = null;
        var film = CatalogueParentKindMapper.ToFilm(podcast, episode);
        if (release is not null)
        {
            film.SetRelease(release);
        }

        return film;
    }

    private static string[] Lines(string post) =>
        post.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    // ---- projection and link -------------------------------------------------------------

    [Fact(DisplayName =
        "When a TV episode is projected for social posting, then it keeps the show's name and handles and is marked as a TV episode, because the templates use the show where a podcast would be.")]
    public void tv_projection_uses_the_show_as_the_parent()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();

        // Act
        var item = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);

        // Assert
        item.ContentKind.Should().Be("TvShowEpisode");
        item.IsCataloguePlayable.Should().BeTrue();
        item.Podcast.Id.Should().Be(show.Id);
        item.Podcast.Name.Should().Be(show.Name);
        item.Podcast.TwitterHandle.Should().Be(show.TwitterHandle);
        item.Episode.Id.Should().Be(tvEpisode.Id);
        item.Episode.PodcastId.Should().Be(show.Id);
        item.Episode.Title.Should().Be(tvEpisode.Title);
        item.Episode.Services.Should().NotBeNull().And.HaveCount(tvEpisode.Services!.Count);
    }

    [Fact(DisplayName =
        "When a podcast episode pair is created, then it is not a catalogue playable, because podcast posts must keep their existing behaviour.")]
    public void podcast_pair_is_not_a_catalogue_playable()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);

        // Act
        var item = new PodcastEpisode(podcast, episode);

        // Assert
        item.ContentKind.Should().BeNull();
        item.IsCataloguePlayable.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a site link is built, then TV and news use the parent name as the slug and a film uses its own name, each with the kind's route and prefixed short id, because that matches the website's cataloguePlayableLink.")]
    public void site_link_matches_the_website_routes()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        var (organisation, report) = CreateNewsReport();
        var film = CreateFilm();

        // Act
        var tv = CataloguePlayableSiteLink.For(CatalogueParentKindMapper.ToPostProjection(tvEpisode, show));
        var news = CataloguePlayableSiteLink.For(CatalogueParentKindMapper.ToPostProjection(report, organisation));
        var filmLink = CataloguePlayableSiteLink.For(CatalogueParentKindMapper.ToPostProjection(film));

        // Assert
        tv.AbsoluteUri.Should().Be("https://cultpodcasts.com" +
                                  CatalogueShortId.PlayablePath(show.Name, tvEpisode.Id, "TvShowEpisode"));
        tv.AbsolutePath.Should().StartWith("/tv/Synthetic%20Cult%20Docs/");
        news.AbsolutePath.Should().StartWith("/news/Synthetic%20Evening%20News/");
        filmLink.AbsolutePath.Should().StartWith("/film/The%20Synthetic%20Prophet/");
        CatalogueShortId.TryDecode(tv.Segments[^1], out var tvId, out var tvKind).Should().BeTrue();
        tvId.Should().Be(tvEpisode.Id);
        tvKind.Should().Be("TvShowEpisode");
    }

    [Fact(DisplayName =
        "When a site link is asked for a podcast episode, then it throws, because podcast posts keep the KV short link.")]
    public void site_link_rejects_podcast_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));

        // Act
        var act = () => CataloguePlayableSiteLink.For(item);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ---- templates ---------------------------------------------------------------------------

    [Fact(DisplayName =
        "When a TV episode is tweeted, then the post names the show with its handle and ends with the /tv site link even with one service and no subjects, because the kind page is the item's home.")]
    public async Task tv_tweet_names_the_show_and_links_the_tv_page()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        var item = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);
        var siteLink = CataloguePlayableSiteLink.For(item);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(item, siteLink);

        // Assert
        var lines = Lines(tweet);
        lines[0].Should().Be($"\"{tvEpisode.Title}\"");
        lines[1].Should().Be($"{show.Name} {show.TwitterHandle}");
        tweet.Should().Contain(item.Episode.Urls.YouTube!.ToString());
        lines[^1].Should().Be(siteLink.AbsoluteUri);
        tweet.Should().NotContain("Synthetic Cult Docs/", "a space in the slug must stay percent-encoded in the link");
    }

    [Fact(DisplayName =
        "When a film with a year-only release is tweeted, then there is no parent-name line and the release shows only the year, because a film has no parent and its release has no day.")]
    public async Task film_tweet_has_no_parent_line_and_a_year_only_release()
    {
        // Arrange
        var film = CreateFilm(CatalogueRelease.FromYear(1998));
        var item = CatalogueParentKindMapper.ToPostProjection(film);
        var siteLink = CataloguePlayableSiteLink.For(item);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(item, siteLink);

        // Assert
        var lines = Lines(tweet);
        lines[0].Should().Be($"\"{film.Name}\"");
        lines[1].Should().Be(film.TwitterHandle);
        lines[2].Should().Be("1998 [1:35:00]");
        tweet.Should().NotContain("1 Jan 1998");
        tweet.Should().NotContain("Synthetic Film Uploads");
        lines[^1].Should().Be(siteLink.AbsoluteUri);
    }

    [Fact(DisplayName =
        "When a film has no known length, then the release line leaves the length out, because a zero length would be wrong.")]
    public async Task film_tweet_omits_an_unknown_length()
    {
        // Arrange
        var film = CreateFilm(CatalogueRelease.FromDate(new DateOnly(2003, 4, 9)), TimeSpan.Zero);
        var item = CatalogueParentKindMapper.ToPostProjection(film);
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(item, CataloguePlayableSiteLink.For(item));

        // Assert
        Lines(tweet).Should().Contain("9 Apr 2003");
        tweet.Should().NotContain("[0:00:00]");
    }

    [Fact(DisplayName =
        "When a news report is posted to Bluesky, then the post names the organisation with its handle and includes the /news site link, because news reports link to their kind page.")]
    public async Task news_bluesky_post_names_the_organisation_and_links_the_news_page()
    {
        // Arrange
        var (organisation, report) = CreateNewsReport();
        var item = CatalogueParentKindMapper.ToPostProjection(report, organisation);
        var siteLink = CataloguePlayableSiteLink.For(item);
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(item, siteLink);

        // Assert
        var lines = Lines(post.Text);
        lines[0].Should().Be($"\"{report.Title}\"");
        lines[1].Should().Be($"{organisation.Name} {organisation.BlueskyHandle}");
        lines.Should().Contain(siteLink.AbsoluteUri);
        post.Url.Should().Be(item.Episode.Urls.YouTube);
    }

    [Fact(DisplayName =
        "When a film is posted to Bluesky, then there is no parent-name line, because a film has no parent.")]
    public async Task film_bluesky_post_has_no_parent_line()
    {
        // Arrange
        var film = CreateFilm(CatalogueRelease.FromYear(2011));
        var item = CatalogueParentKindMapper.ToPostProjection(film);
        var sut = _mocker.CreateInstance<BlueskyEmbedCardPostFactory>();

        // Act
        var post = await sut.Create(item, CataloguePlayableSiteLink.For(item));

        // Assert
        var lines = Lines(post.Text);
        lines[0].Should().Be($"\"{film.Name}\"");
        lines[1].Should().StartWith("2011");
        post.Text.Should().NotContain("Synthetic Film Uploads");
        post.Text.Should().Contain("/film/");
    }

    [Fact(DisplayName =
        "When a podcast episode with one service and no subjects is tweeted, then the short link is still left off, because podcast templates are unchanged.")]
    public async Task podcast_tweet_is_unchanged()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => { p.HashTag = null; p.EnrichmentHashTags = null; });
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        episode.Subjects = [];
        episode.Guests = null;
        var shortUrl = new Uri($"https://s.cultpodcasts.com/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<TweetBuilder>();

        // Act
        var tweet = await sut.BuildTweet(new PodcastEpisode(podcast, episode), shortUrl);

        // Assert
        tweet.Should().NotContain(shortUrl.ToString());
        Lines(tweet)[1].Should().StartWith(podcast.Name);
        Lines(tweet)[2].Should().Be(
            $"{episode.ReleaseUtc.ToString(TweetBuilder.ReleaseFormat)} {episode.Length.ToString(TweetBuilder.LengthFormat, System.Globalization.CultureInfo.InvariantCulture)}");
    }

    // ---- posted state ------------------------------------------------------------------------

    [Fact(DisplayName =
        "When a TV episode tweet is sent, then the tweeted flag is written through the catalogue writer and no podcast episode is saved, because a projection must never land in the episodes container.")]
    public async Task tv_tweet_marks_the_tv_episode_not_an_episode()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        var item = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync("tweet");
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send("tweet"))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, CataloguePlayableSiteLink.For(item));

        // Assert
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.MarkTweeted(item), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a podcast episode tweet is sent, then the episode is saved as before and the catalogue writer is not used, because podcast posting is unchanged.")]
    public async Task podcast_tweet_still_saves_the_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        _mocker.GetMock<ITweetBuilder>()
            .Setup(x => x.BuildTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync("tweet");
        _mocker.GetMock<ITwitterClient>()
            .Setup(x => x.Send("tweet"))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<TweetPoster>();

        // Act
        await sut.PostTweet(item, null);

        // Assert
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.Is<Episode>(e => e.Tweeted)), Times.Once);
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Verify(x => x.MarkTweeted(It.IsAny<PodcastEpisode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a film is posted to Bluesky, then its AT URI is written through the catalogue writer and no podcast episode is saved, because the film document owns the post state.")]
    public async Task film_bluesky_post_marks_the_film()
    {
        // Arrange
        const string atUri = "at://did:plc:synthetic/app.bsky.feed.post/film";
        var item = CatalogueParentKindMapper.ToPostProjection(CreateFilm());
        _mocker.GetMock<IBlueskyEmbedCardPostFactory>()
            .Setup(x => x.Create(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new BlueskyEmbedCardPost("text", item.Episode.Urls.YouTube!, Service.YouTube));
        _mocker.GetMock<IBlueskyFeedClient>()
            .Setup(x => x.PostOpenGraphCard(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<Uri?>()))
            .ReturnsAsync(atUri);
        var sut = _mocker.CreateInstance<BlueskyPoster>();

        // Act
        var status = await sut.Post(item, CataloguePlayableSiteLink.For(item));

        // Assert
        status.Should().Be(BlueskySendStatus.Success);
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.MarkBlueskyPosted(item, atUri), Times.Once);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    // ---- run orchestration -------------------------------------------------------------------

    [Fact(DisplayName =
        "When no podcast episode is waiting to be tweeted, then a waiting TV episode is tweeted with its /tv site link and the podcast shortener is not used, because the KV short link is podcast-shaped.")]
    public async Task tweeter_falls_back_to_a_catalogue_item_with_its_site_link()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        var item = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetUntweetedPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync([]);
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Setup(x => x.GetUntweeted()).ReturnsAsync([item]);
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(item, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<ITweetPoster>()
            .Verify(x => x.PostTweet(item, CataloguePlayableSiteLink.For(item), false), Times.Once);
        _mocker.GetMock<IShortnerService>()
            .Verify(x => x.Write(It.IsAny<PodcastEpisode>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a podcast episode is tweeted, then catalogue items are not looked up in that run, because the run still sends one tweet.")]
    public async Task tweeter_prefers_podcast_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var podcastItem = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetUntweetedPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync([podcastItem]);
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(podcastItem, It.IsAny<bool>()))
            .ReturnsAsync(new Cloudflare.Models.WriteResult(true, new Uri("https://s.cultpodcasts.com/abc")));
        _mocker.GetMock<ITweetPoster>()
            .Setup(x => x.PostTweet(podcastItem, It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(new PostTweetResponse(TweetSendStatus.Sent));
        var sut = _mocker.CreateInstance<Tweeter>();

        // Act
        await sut.Tweet(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<ICataloguePlayableSocialPosts>().Verify(x => x.GetUntweeted(), Times.Never);
    }

    [Fact(DisplayName =
        "When a Bluesky run has room left after podcast episodes, then catalogue items are posted with their site links up to max-posts, because they share the run's budget.")]
    public async Task bluesky_run_posts_catalogue_items_within_max_posts()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var podcastItem = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        var (organisation, report) = CreateNewsReport();
        var newsItem = CatalogueParentKindMapper.ToPostProjection(report, organisation);
        var filmItem = CatalogueParentKindMapper.ToPostProjection(CreateFilm());
        _mocker.GetMock<IPodcastEpisodeProvider>()
            .Setup(x => x.GetBlueskyReadyPodcastEpisodes(It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<PodcastEpisode>?>()))
            .ReturnsAsync([podcastItem]);
        _mocker.GetMock<IShortnerService>()
            .Setup(x => x.Write(podcastItem, It.IsAny<bool>()))
            .ReturnsAsync(new Cloudflare.Models.WriteResult(true, new Uri("https://s.cultpodcasts.com/abc")));
        _mocker.GetMock<ICataloguePlayableSocialPosts>()
            .Setup(x => x.GetBlueskyReady())
            .ReturnsAsync([newsItem, filmItem]);
        _mocker.GetMock<IBlueskyPoster>()
            .Setup(x => x.Post(It.IsAny<PodcastEpisode>(), It.IsAny<Uri?>(), It.IsAny<bool>()))
            .ReturnsAsync(BlueskySendStatus.Success);
        var sut = _mocker.CreateInstance<BlueskyPostManager>();

        // Act
        await sut.Post(youTubeRefreshed: true, spotifyRefreshed: true);

        // Assert
        _mocker.GetMock<IBlueskyPoster>().Verify(x => x.Post(podcastItem, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Once);
        _mocker.GetMock<IBlueskyPoster>()
            .Verify(x => x.Post(newsItem, CataloguePlayableSiteLink.For(newsItem), false), Times.Once);
        _mocker.GetMock<IBlueskyPoster>().Verify(x => x.Post(filmItem, It.IsAny<Uri?>(), It.IsAny<bool>()), Times.Never);
        _mocker.GetMock<IShortnerService>()
            .Verify(x => x.Write(newsItem, It.IsAny<bool>()), Times.Never);
    }

    // ---- candidates and writer ---------------------------------------------------------------

    [Fact(DisplayName =
        "When catalogue tweet candidates are read, then only active, untweeted items with a postable link are returned newest first, because the podcast candidate rules apply to every kind.")]
    public async Task catalogue_untweeted_candidates_follow_the_podcast_rules()
    {
        // Arrange
        var (show, newest) = CreateTvEpisode();
        newest.SetRelease(CatalogueRelease.FromDateTimeUtc(DomainTestFixture.UtcDaysAgo(0).AddHours(1)));
        var tweeted = CreateTvEpisode().Episode;
        tweeted.Tweeted = true;
        var ignored = CreateTvEpisode().Episode;
        ignored.Ignored = true;
        var noLink = CreateTvEpisode().Episode;
        noLink.Services = null;
        var (organisation, report) = CreateNewsReport();
        var film = CreateFilm();
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(() => new[] { show }.ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetByTvShowId(show.Id, It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns(() => new[] { tweeted, ignored, noLink, newest }.ToAsyncEnumerable());
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => new[] { organisation }.ToAsyncEnumerable());
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetByNewsOrganisationId(organisation.Id, It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns(() => new[] { report }.ToAsyncEnumerable());
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns(() => new[] { film }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<CataloguePlayableSocialPosts>();

        // Act
        var result = await sut.GetUntweeted();

        // Assert
        result.Select(x => x.Episode.Id).Should().Equal(newest.Id, report.Id, film.Id);
        result.Select(x => x.ContentKind).Should().Equal("TvShowEpisode", "NewsReport", "Film");
    }

    [Fact(DisplayName =
        "When a TV episode is marked tweeted, then the stored TV episode is reloaded by show and id and saved with tweeted set, because the writer must update the TV episode document.")]
    public async Task mark_tweeted_saves_the_tv_episode()
    {
        // Arrange
        var (show, tvEpisode) = CreateTvEpisode();
        var item = CatalogueParentKindMapper.ToPostProjection(tvEpisode, show);
        TvShowEpisode? saved = null;
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetTvShowEpisode(show.Id, tvEpisode.Id))
            .ReturnsAsync(tvEpisode);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(x => saved = x)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<CataloguePlayableSocialPosts>();

        // Act
        await sut.MarkTweeted(item);

        // Assert
        saved.Should().BeSameAs(tvEpisode);
        saved!.Tweeted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a news report is marked posted to Bluesky, then the stored report is saved with the AT URI, because a later delete needs that URI.")]
    public async Task mark_bluesky_posted_saves_the_news_report()
    {
        // Arrange
        const string atUri = "at://did:plc:synthetic/app.bsky.feed.post/news";
        var (organisation, report) = CreateNewsReport();
        var item = CatalogueParentKindMapper.ToPostProjection(report, organisation);
        NewsReport? saved = null;
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetNewsReport(organisation.Id, report.Id))
            .ReturnsAsync(report);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(x => saved = x)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<CataloguePlayableSocialPosts>();

        // Act
        await sut.MarkBlueskyPosted(item, atUri);

        // Assert
        saved.Should().BeSameAs(report);
        saved!.BlueskyPost.Should().Be(atUri);
    }

    [Fact(DisplayName =
        "When a film is marked tweeted, then the stored film is saved with tweeted set, because the film document owns its post state.")]
    public async Task mark_tweeted_saves_the_film()
    {
        // Arrange
        var film = CreateFilm();
        var item = CatalogueParentKindMapper.ToPostProjection(film);
        Film? saved = null;
        _mocker.GetMock<IFilmRepository>().Setup(x => x.GetFilm(film.Id)).ReturnsAsync(film);
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.Save(It.IsAny<Film>()))
            .Callback<Film>(x => saved = x)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<CataloguePlayableSocialPosts>();

        // Act
        await sut.MarkTweeted(item);

        // Assert
        saved.Should().BeSameAs(film);
        saved!.Tweeted.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the catalogue writer is given a podcast episode, then it throws, because podcast episodes are saved through the episode repository.")]
    public async Task writer_rejects_podcast_episodes()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var item = new PodcastEpisode(podcast, _fixture.CreateStoredEpisode(podcast));
        var sut = _mocker.CreateInstance<CataloguePlayableSocialPosts>();

        // Act
        var act = () => sut.MarkTweeted(item);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
