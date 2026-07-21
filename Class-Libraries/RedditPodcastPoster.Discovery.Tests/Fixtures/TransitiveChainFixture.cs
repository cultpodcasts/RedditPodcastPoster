using AutoFixture;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.Tests.Fixtures;

internal static class TransitiveChainFixture
{
    public const string KanunguEpisodeName = "The Kanungu Cult: When the World Was Supposed to End";
    public const string KanunguShowName = "True Crime With Reni";

    public static readonly Uri KanunguYouTubeUrl = new("https://www.youtube.com/watch?v=YO4eA110hP0");
    public static readonly Uri KanunguSpotifyUrl = new("https://open.spotify.com/episode/3vk2JpHOhiYH3GTuelvrWq");
    public static readonly Uri NbaAppleUrl = new(
        "https://podcasts.apple.com/podcast/crossover-episode-hornets-and-timberwolves-trade/id995386468?i=1000774379000");

    public static readonly Guid KanunguYouTubeRowId = Guid.Parse("73325c50-d53e-42dd-b2b6-9d551590adcc");
    public static readonly Guid KanunguTaddyRowId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    public static readonly Guid NbaBridgeRowId = Guid.Parse("c0ffee00-0000-4000-8000-000000000001");
    public static readonly Guid LittleLiterId = Guid.Parse("2cfd4d11-0a00-48e9-b137-1991a63a7dbe");
    public static readonly Guid ConservativeCultId = Guid.Parse("b851a85e-9bb5-45e8-9934-3bcc692e54f6");
    public static readonly Guid ObamaSpeechId = Guid.Parse("490f9a6a-1531-4a63-bf1d-bda35c527f1f");
    public static readonly Guid MonsterClubId = Guid.Parse("8f8a8145-71aa-4bde-9300-eca7306e249f");

    private static readonly Fixture MetadataFixture = CreateMetadataFixture();

    public static IReadOnlyList<DiscoveryResult> Create() =>
    [
        BuildKanunguYouTubeRow(),
        BuildKanunguTaddyRow(),
        BuildNbaBridgeRow(),
        BuildLittleLiterRow(),
        BuildConservativeCultRow(),
        BuildObamaSpeechRow(),
        BuildMonsterClubRow()
    ];

    private static Fixture CreateMetadataFixture()
    {
        var fixture = new Fixture();
        fixture.Customize<DiscoveryResult>(composer => composer
            .Without(x => x.Id)
            .Without(x => x.EpisodeName)
            .Without(x => x.ShowName)
            .Without(x => x.Urls)
            .Without(x => x.Subjects)
            .Without(x => x.Sources)
            .Without(x => x.AcceptProbability)
            .Without(x => x.AutoHidden)
            .Without(x => x.YouTubeViews)
            .Without(x => x.YouTubeChannelMembers)
            .Without(x => x.EnrichedTimeFromApple)
            .Without(x => x.EnrichedUrlFromSpotify)
            .Without(x => x.MatchingPodcastIds));
        return fixture;
    }

    private static DiscoveryResult BuildKanunguYouTubeRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = KanunguYouTubeRowId;
        row.EpisodeName = KanunguEpisodeName;
        row.ShowName = KanunguShowName;
        row.Released = DateTime.Parse("2026-06-26T18:00:06Z");
        row.Length = TimeSpan.Parse("00:06:00");
        row.Urls = new DiscoveryResultUrls
        {
            YouTube = KanunguYouTubeUrl
        };
        row.Subjects = ["Movement for the Restoration of the Ten Commandments of God", "Cult Psychology"];
        row.YouTubeViews = 33;
        row.YouTubeChannelMembers = 401;
        row.ImageUrl = new Uri("https://i.ytimg.com/vi/YO4eA110hP0/maxresdefault.jpg");
        row.Sources = [DiscoverService.YouTube];
        row.AcceptProbability = 0.05f;
        row.AutoHidden = true;
        return row;
    }

    private static DiscoveryResult BuildKanunguTaddyRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = KanunguTaddyRowId;
        row.EpisodeName = KanunguEpisodeName;
        row.ShowName = KanunguShowName;
        row.Released = DateTime.Parse("2026-06-26T15:19:26Z");
        row.Length = TimeSpan.Parse("00:06:00");
        row.Urls = new DiscoveryResultUrls
        {
            Spotify = KanunguSpotifyUrl,
            YouTube = KanunguYouTubeUrl
        };
        row.Subjects = ["Movement for the Restoration of the Ten Commandments of God"];
        row.ImageUrl = new Uri("https://i.scdn.co/image/ab6765630000ba8a8e5a66a755975e572c09369d");
        row.Sources = [DiscoverService.Taddy];
        row.EnrichedUrlFromSpotify = true;
        row.AcceptProbability = 0.04f;
        row.AutoHidden = true;
        return row;
    }

    private static DiscoveryResult BuildNbaBridgeRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = NbaBridgeRowId;
        row.EpisodeName = "Crossover episode: Hornets and Timberwolves trade";
        row.ShowName = "Locked On NBA";
        row.Released = DateTime.Parse("2026-06-26T14:00:00Z");
        row.Length = TimeSpan.Parse("00:45:00");
        row.Urls = new DiscoveryResultUrls
        {
            Spotify = KanunguSpotifyUrl,
            Apple = NbaAppleUrl
        };
        row.Subjects =
        [
            "Nine O'Clock Service",
            "School of Philosophy and Economic Science",
            "Human Trafficking",
            "Adolfo Constanzo",
            "Peoples Temple",
            "Scientology",
            "Theranos",
            "Corporate Cult"
        ];
        row.Sources = [DiscoverService.Taddy];
        row.EnrichedTimeFromApple = true;
        row.EnrichedUrlFromSpotify = true;
        row.AcceptProbability = 0.9873123f;
        row.AutoHidden = false;
        return row;
    }

    private static DiscoveryResult BuildLittleLiterRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = LittleLiterId;
        row.EpisodeName = "Little Liter: The Girl Who Didn't Go To Paris";
        row.ShowName = "Cult Liter with Spencer Henry";
        row.Released = DateTime.Parse("2026-06-26T16:00:00Z");
        row.Length = TimeSpan.Parse("00:25:39");
        row.Urls = new DiscoveryResultUrls
        {
            Spotify = new Uri("https://open.spotify.com/episode/1VxfdrdZ3UVuILrY1Eqy73"),
            Apple = new Uri("https://podcasts.apple.com/podcast/little-liter-the-girl-who-didnt-go-to-paris/id1436376574?i=1000774367978")
        };
        row.Subjects = [];
        row.Sources = [DiscoverService.Taddy];
        row.EnrichedTimeFromApple = true;
        row.EnrichedUrlFromSpotify = true;
        row.AcceptProbability = 0.020087002f;
        row.AutoHidden = true;
        return row;
    }

    private static DiscoveryResult BuildConservativeCultRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = ConservativeCultId;
        row.EpisodeName = "Conservative Cult Bibles";
        row.ShowName = "Sunday - PM on SermonAudio";
        row.Released = DateTime.Parse("2026-06-26T17:27:45");
        row.Length = TimeSpan.Parse("00:45:00");
        row.Urls = new DiscoveryResultUrls();
        row.Subjects = [];
        row.Sources = [DiscoverService.Taddy];
        row.AcceptProbability = 0.00003197629f;
        row.AutoHidden = true;
        return row;
    }

    private static DiscoveryResult BuildObamaSpeechRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = ObamaSpeechId;
        row.EpisodeName = "How the \"cult of success\" created Trump and Musk - Obama's speechwriter";
        row.ShowName = "Ways to Change the World with Krishnan Guru-Murthy";
        row.Released = DateTime.Parse("2026-06-26T12:23:00");
        row.Length = TimeSpan.Parse("00:44:07");
        row.Urls = new DiscoveryResultUrls
        {
            Spotify = new Uri("https://open.spotify.com/episode/272XKL2aDYGYIEEkR1ehLz")
        };
        row.Subjects = ["_America"];
        row.Sources = [DiscoverService.Taddy];
        row.EnrichedUrlFromSpotify = true;
        row.AcceptProbability = 0.000030145211f;
        row.AutoHidden = true;
        return row;
    }

    private static DiscoveryResult BuildMonsterClubRow()
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = MonsterClubId;
        row.EpisodeName = "The Monster Club (1981)   Monsters Rule! O.K.?!";
        row.ShowName = "They Came From Within (Cult Movie Reviews)";
        row.Released = DateTime.Parse("2026-06-26T11:53:06Z");
        row.Length = TimeSpan.Parse("00:11:54");
        row.Urls = new DiscoveryResultUrls
        {
            Spotify = new Uri("https://open.spotify.com/episode/2BDhspl10o6reFz3R7FdGO"),
            Apple = new Uri("https://podcasts.apple.com/podcast/the-monster-club-1981-monsters-rule-o-k/id1651090346?i=1000774340016")
        };
        row.Subjects = [];
        row.Sources = [DiscoverService.Taddy];
        row.EnrichedTimeFromApple = true;
        row.EnrichedUrlFromSpotify = true;
        row.AcceptProbability = 0.000025498652f;
        row.AutoHidden = true;
        return row;
    }
}
