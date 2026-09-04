namespace Api.Models;

public class SubmitUrlExtractRequest
{
    public required Uri Url { get; set; }

    public required string Html { get; set; }

    public bool HasUsableHttpUrl() => SubmitUrlRequest.IsUsableHttpUrl(Url);
}
