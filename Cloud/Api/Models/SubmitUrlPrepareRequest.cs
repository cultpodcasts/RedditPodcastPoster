namespace Api.Models;

public class SubmitUrlPrepareRequest
{
    public required Uri Url { get; set; }

    public bool HasUsableHttpUrl() => SubmitUrlRequest.IsUsableHttpUrl(Url);
}
