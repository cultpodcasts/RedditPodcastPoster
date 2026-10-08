namespace Api.Dtos;

public class SubmitUrlExtractRequest
{
    public required Uri Url { get; set; }

    public required string Html { get; set; }
}
