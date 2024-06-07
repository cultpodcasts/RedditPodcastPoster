using FluentAssertions;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests;

public class AppleIdResolverTests
{
    [Theory]
    [InlineData(
        @"https://podcasts.apple.com/podcast/sssssssssss/id1207679582?i=1300656649769",
        1207679582L)]
    [InlineData(
        @"https://podcasts.apple.com/us/podcast/sssssssssss/id1207679582?i=1300656649769",
        1207679582L)]
    [InlineData(
        @"https://podcasts.apple.com/us/podcast/xxxxx-12-yyyy-zzzzz-ddddddd/id1207679582?i=1300656649769",
        1207679582L)]
    [InlineData(
        @"https://podcasts.apple.com/us/podcast/xxxxx-12-yyyy-zzzzz-ddddddd-p%24rn/id1207679582?i=1300656649769",
        1207679582L)]
    public void GetPodcastId_WithEscapeCharactersInUrl_IsCorrect(string url, long? expected)
    {
        // arrange
        // act
        var result = AppleIdResolver.GetPodcastId(new Uri(url));
        // assert
        result.Should().Be(expected);
    }
}