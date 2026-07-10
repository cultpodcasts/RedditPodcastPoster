using CommandLine;

namespace PeopleReviewer;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        return await Parser.Default.ParseArguments<PeopleReviewRequest>(args)
            .MapResult(async request =>
            {
                try
                {
                    await PeopleReviewServer.RunAsync(request);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }
            }, _ => Task.FromResult(-1));
    }
}
