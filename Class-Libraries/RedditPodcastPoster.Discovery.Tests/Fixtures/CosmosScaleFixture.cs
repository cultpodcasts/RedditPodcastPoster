using AutoFixture;
using RedditPodcastPoster.Models.Discovery;

namespace RedditPodcastPoster.Discovery.Tests.Fixtures;

internal static class CosmosScaleFixture
{
    public const string DocumentId = "d6d38104-5ba5-4dec-ada4-094c48f61808";
    public const int RawRowCount = 95;
    private const int Seed = 42;

    private static readonly Fixture MetadataFixture = CreateMetadataFixture();

    public static IReadOnlyList<DiscoveryResult> Create() =>
        Enumerable.Range(0, RawRowCount).Select(BuildRow).ToList();

    private static Fixture CreateMetadataFixture()
    {
        var fixture = new Fixture { RepeatCount = 1 };
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

    private static DiscoveryResult BuildRow(int index)
    {
        var row = MetadataFixture.Create<DiscoveryResult>();
        row.Id = CreateDeterministicId(index);
        row.ShowName = $"Cosmos Scale Show {index:D3}";
        row.EpisodeName = $"Cosmos Scale Episode {index:D3}";
        row.Urls = BuildUniqueUrls(index);
        row.Subjects = index % 7 == 0 ? [$"Subject-{index:D3}"] : [];
        row.Sources = [DiscoverService.Taddy];
        row.AcceptProbability = 0.00001f * (index + 1);
        row.AutoHidden = true;
        row.EnrichedTimeFromApple = index % 3 == 0;
        row.EnrichedUrlFromSpotify = index % 2 == 0;
        return row;
    }

    private static Guid CreateDeterministicId(int index) =>
        Guid.Parse($"00000000-0000-4000-8000-{index + Seed:x12}");

    private static DiscoveryResultUrls BuildUniqueUrls(int index)
    {
        return (index % 5) switch
        {
            0 => new DiscoveryResultUrls(),
            1 => new DiscoveryResultUrls
            {
                Spotify = CreateUniqueSpotifyUrl(index)
            },
            2 => new DiscoveryResultUrls
            {
                Apple = CreateUniqueAppleUrl(index)
            },
            3 => new DiscoveryResultUrls
            {
                YouTube = CreateUniqueYouTubeUrl(index)
            },
            _ => new DiscoveryResultUrls
            {
                Spotify = CreateUniqueSpotifyUrl(index),
                Apple = CreateUniqueAppleUrl(index),
                YouTube = CreateUniqueYouTubeUrl(index)
            }
        };
    }

    private static Uri CreateUniqueSpotifyUrl(int index) =>
        new($"https://open.spotify.com/episode/cosmos{index:D3}{new string('a', 15)}");

    private static Uri CreateUniqueAppleUrl(int index) =>
        new(
            $"https://podcasts.apple.com/podcast/cosmos-scale-episode-{index:D3}/id{1000000000 + index}?i={1000000000000L + index}");

    private static Uri CreateUniqueYouTubeUrl(int index) =>
        new($"https://www.youtube.com/watch?v={index.ToString().PadLeft(11, '0')}");
}
