using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.Twitter.Auth;
using RedditPodcastPoster.Twitter.Extensions;
using XOAuth2Authorize;

// One-off: OAuth 2.0 PKCE login for the X posting account. This is the recovery path after OAuth 1.0a ends on
// 1 Jan 2027. Dry run by default. Never prints the code verifier, the authorization code, or token values.
if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Environment.ContentRootPath = Directory.GetCurrentDirectory();

builder.Configuration
    .AddJsonFile("appsettings.json", true)
    .AddEnvironmentVariables("RedditPodcastPoster_")
    .AddCommandLine(args)
    .AddSecrets(Assembly.GetExecutingAssembly());

builder.Services
    .AddLogging()
    .AddRepositories()
    .AddXOAuth2TokenServices();

using var host = builder.Build();

return await Parser.Default.ParseArguments<XOAuth2AuthorizeRequest>(args)
    .MapResult(async request => await Run(request),
        _ => Task.FromResult(-1));

async Task<int> Run(XOAuth2AuthorizeRequest request)
{
    if (!Uri.TryCreate(request.RedirectUri, UriKind.Absolute, out var redirect))
    {
        Console.WriteLine("The redirect URI is not an absolute URI.");
        return 1;
    }

    using var scope = host.Services.CreateScope();
    var authorizer = scope.ServiceProvider.GetRequiredService<IXOAuth2Authorizer>();
    var begun = await authorizer.Begin(request.Execute, redirect, CancellationToken.None);
    foreach (var line in begun.Report)
    {
        Console.WriteLine(line);
    }

    if (begun.Session is null)
    {
        return begun.Succeeded ? 0 : 1;
    }

    Console.WriteLine(begun.Session.AuthorizeUrl);
    string? callback;
    if (request.Manual)
    {
        Console.WriteLine("Paste the full callback URL and press Enter.");
        callback = Console.ReadLine();
    }
    else
    {
        try
        {
            OpenBrowser(begun.Session.AuthorizeUrl);
            Console.WriteLine($"Listening on {redirect.GetLeftPart(UriPartial.Authority)}/ for up to 5 minutes.");
            callback = await WaitForCallback(redirect, TimeSpan.FromMinutes(5));
        }
        catch (HttpListenerException)
        {
            Console.WriteLine("The loopback listener did not start. Paste the full callback URL and press Enter.");
            callback = Console.ReadLine();
        }
        catch (OperationCanceledException)
        {
            await authorizer.Abandon(begun.Session);
            Console.WriteLine("The wait for consent ended. Nothing was saved.");
            return 1;
        }
    }

    if (string.IsNullOrWhiteSpace(callback))
    {
        await authorizer.Abandon(begun.Session);
        Console.WriteLine("No callback was received. The refresh lease was released. Nothing was saved.");
        return 1;
    }

    var completed = await authorizer.Complete(begun.Session, callback, CancellationToken.None);
    foreach (var line in completed.Report)
    {
        Console.WriteLine(line);
    }

    return completed.Succeeded ? 0 : 1;
}

static void OpenBrowser(Uri url)
{
    try
    {
        Process.Start(new ProcessStartInfo(url.AbsoluteUri) {UseShellExecute = true});
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Could not open a browser ({ex.GetType().Name}). Open the URL above yourself.");
    }
}

static async Task<string?> WaitForCallback(Uri redirect, TimeSpan timeout)
{
    using var listener = new HttpListener();
    listener.Prefixes.Add(redirect.GetLeftPart(UriPartial.Authority) + "/");
    listener.Start();
    using var cancel = new CancellationTokenSource(timeout);
    try
    {
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancel.Token);
            if (!IsRedirectPath(redirect, context.Request.Url))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                continue;
            }

            var code = context.Request.QueryString["code"];
            var state = context.Request.QueryString["state"];
            var error = context.Request.QueryString["error"];
            if (string.IsNullOrEmpty(code) && string.IsNullOrEmpty(error))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
                continue;
            }

            var page = Encoding.UTF8.GetBytes("<html><body><p>You can close this window.</p></body></html>");
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = page.Length;
            await context.Response.OutputStream.WriteAsync(page);
            context.Response.Close();
            if (!string.IsNullOrEmpty(error))
            {
                return "error=" + Uri.EscapeDataString(error) +
                       (string.IsNullOrEmpty(state) ? "" : "&state=" + Uri.EscapeDataString(state));
            }

            return "code=" + Uri.EscapeDataString(code!) + "&state=" + Uri.EscapeDataString(state ?? "");
        }
    }
    finally
    {
        listener.Stop();
    }
}

static bool IsRedirectPath(Uri redirect, Uri? requestUrl)
{
    if (requestUrl is null)
    {
        return false;
    }

    static string Normalize(string path)
    {
        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed;
    }

    return string.Equals(Normalize(requestUrl.AbsolutePath), Normalize(redirect.AbsolutePath), StringComparison.Ordinal);
}
