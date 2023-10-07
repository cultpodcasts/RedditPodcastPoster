namespace SubmitUrl;

public interface ISubmitUrlProcessor
{
    Task Process(SubmitUrlRequest request);
}