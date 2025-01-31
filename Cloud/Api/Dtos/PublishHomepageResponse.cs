using System.Text.Json.Serialization;
using RedditPodcastPoster.ContentPublisher.Models;

namespace Api.Dtos;

public class PublishHomepageResponse
{
    [JsonPropertyName("homepagePublished")]
    public bool HomepagePublished { get; set; }


    [JsonPropertyName("preProcessedHomepagePublished")]
    public bool PreProcessedHomepagePublished { get; set; }

    public static PublishHomepageResponse ToDto(PublishHomepageResult result)
    {
        return new PublishHomepageResponse
        {
            HomepagePublished = result.HomepagePublished,
            PreProcessedHomepagePublished = result.PreProcessedHomepagePublished
        };
    }
}