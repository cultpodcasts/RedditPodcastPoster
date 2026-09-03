using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.BBC.Tests.BusinessRules;

public class SoundsPageMetaDataExtractorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Sounds extract prefers aod_play_area over an earlier aod_tracks experience, " +
        "so episode title and brand series come from the play-area programme rather than the music-tracks rail.")]
    public async Task prefers_play_area_over_earlier_tracks_experience()
    {
        // Arrange
        var programmeId = _fixture.CreateYouTubeId();
        var tracksTitle = _fixture.CreateTitle();
        var episodeTitle = _fixture.CreateTitle();
        var brandName = _fixture.CreateTitle();
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{programmeId}");
        var html = BuildSoundsHtml(
            programmeId,
            tracksExperience: (tracksTitle, tracksTitle),
            playAreaExperience: (episodeTitle, brandName));
        using var response = OkHtml(html);
        var sut = _mocker.CreateInstance<SoundsPageMetaDataExtractor>();

        // Act
        var meta = await sut.Extract(url, response);

        // Assert
        meta.Title.Should().Be(episodeTitle);
        meta.ShowName.Should().Be(brandName);
        meta.Title.Should().NotBe(tracksTitle);
        meta.Publisher.Should().Be("BBC");
    }

    [Fact(DisplayName =
        "Sounds brand container equal to the episode title leaves ShowName null, " +
        "so lookup does not prefill podcastName with a one-off brand that is the episode itself.")]
    public async Task brand_one_off_leaves_show_name_null()
    {
        // Arrange
        var programmeId = _fixture.CreateYouTubeId();
        var oneOffTitle = _fixture.CreateTitle();
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{programmeId}");
        var html = BuildSoundsHtml(
            programmeId,
            tracksExperience: null,
            playAreaExperience: (oneOffTitle, oneOffTitle));
        using var response = OkHtml(html);
        var sut = _mocker.CreateInstance<SoundsPageMetaDataExtractor>();

        // Act
        var meta = await sut.Extract(url, response);

        // Assert
        meta.Title.Should().Be(oneOffTitle);
        meta.ShowName.Should().BeNull();
    }

    private static string BuildSoundsHtml(
        string programmeId,
        (string EpisodeTitle, string BrandName)? tracksExperience,
        (string EpisodeTitle, string BrandName) playAreaExperience)
    {
        var experiences = new List<object>();
        if (tracksExperience is { } tracks)
        {
            experiences.Add(Experience("aod_tracks", tracks.EpisodeTitle, tracks.BrandName));
        }

        experiences.Add(Experience("aod_play_area", playAreaExperience.EpisodeTitle, playAreaExperience.BrandName));

        var nextData = new
        {
            props = new
            {
                isInUK = true,
                pageProps = new
                {
                    dehydratedState = new
                    {
                        queries = new[]
                        {
                            new
                            {
                                queryKey = new[] { $"programme-{programmeId}" },
                                state = new
                                {
                                    data = new
                                    {
                                        data = experiences.ToArray()
                                    }
                                }
                            }
                        }
                    }
                }
            },
            page = "/play",
            query = new { programmeId }
        };

        var json = JsonSerializer.Serialize(nextData);
        return $"<html><head><script id=\"__NEXT_DATA__\" type=\"application/json\">{json}</script></head><body></body></html>";
    }

    private static object Experience(string id, string episodeTitle, string brandTitle) =>
        new
        {
            id,
            data = new[]
            {
                new
                {
                    titles = new
                    {
                        primary = brandTitle,
                        secondary = string.Equals(brandTitle, episodeTitle, StringComparison.Ordinal)
                            ? null
                            : episodeTitle
                    },
                    container = new
                    {
                        type = "brand",
                        title = brandTitle
                    }
                }
            }
        };

    private static HttpResponseMessage OkHtml(string html) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };
}
