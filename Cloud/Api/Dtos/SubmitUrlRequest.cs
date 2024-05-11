namespace Api.Dtos;

public class SubmitUrlRequest
{
    public required Uri Url { get; set; }

    public Guid? PodcastId { get; set; }
}