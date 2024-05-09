namespace Indexer.Dtos;

public class SubmitUrlResponse
{
    public string? Success { get; private set; }
    public string? Error { get; private set; }

    public static SubmitUrlResponse Successful(string message)
    {
        return new SubmitUrlResponse() {Success = message};
    }
    public static SubmitUrlResponse Failure(string message)
    {
        return new SubmitUrlResponse() { Error = message };
    }
}