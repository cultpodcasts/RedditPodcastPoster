using Azure;
using FluentAssertions;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class ActivityBookingTtlRules
{
    [Fact(DisplayName =
        "The bookActivity JS reference copy declares initiate/complete TTL constants matching ActivityBookingTtl " +
        "(2d then 7d) and stamps them on createDocument (ttl) and replaceDocument (doc.ttl), because the sproc " +
        "is what writes item TTL so the initiate/complete ledger stays and completed rows cannot accumulate forever (#1302).")]
    public void book_activity_js_stamps_ttl_on_create_and_replace()
    {
        // Arrange
        var repoRoot = FindRepoRoot();
        var jsPath = Path.Combine(repoRoot, "Azure-Resources", "CosmosDb", "bookActivity.js");
        var js = File.ReadAllText(jsPath);

        // Act
        var hasInitiateConstant = js.Contains($"initiateTtlSeconds = {ActivityBookingTtl.InitiateSeconds}",
            StringComparison.Ordinal);
        var hasCompleteConstant = js.Contains($"completeTtlSeconds = {ActivityBookingTtl.CompleteSeconds}",
            StringComparison.Ordinal);
        var stampsInitiateOnCreate =
            js.Contains("'ttl': initiateTtlSeconds", StringComparison.Ordinal) ||
            js.Contains("\"ttl\": initiateTtlSeconds", StringComparison.Ordinal) ||
            js.Contains("ttl: initiateTtlSeconds", StringComparison.Ordinal);
        var stampsCompleteOnReplace = js.Contains("doc.ttl = completeTtlSeconds", StringComparison.Ordinal);

        // Assert
        ActivityBookingTtl.CompleteSeconds.Should().BeGreaterThan(ActivityBookingTtl.InitiateSeconds);
        TimeSpan.FromSeconds(ActivityBookingTtl.InitiateSeconds).Should().Be(TimeSpan.FromDays(2));
        TimeSpan.FromSeconds(ActivityBookingTtl.CompleteSeconds).Should().Be(TimeSpan.FromDays(7));
        hasInitiateConstant.Should().BeTrue(
            "bookActivity.js must assign initiateTtlSeconds = {0}", ActivityBookingTtl.InitiateSeconds);
        hasCompleteConstant.Should().BeTrue(
            "bookActivity.js must assign completeTtlSeconds = {0}", ActivityBookingTtl.CompleteSeconds);
        stampsInitiateOnCreate.Should().BeTrue(
            "bookActivity.js createDocument path must stamp ttl from initiateTtlSeconds");
        stampsCompleteOnReplace.Should().BeTrue(
            "bookActivity.js replaceDocument path must assign doc.ttl = completeTtlSeconds");
    }

    [Fact(DisplayName =
        "The Activity container bicep enables opt-in TTL (defaultTtl -1) and the embedded bookActivity sproc " +
        "declares the same initiate/complete TTL constants as ActivityBookingTtl and stamps them on " +
        "createDocument (ttl) and replaceDocument (doc.ttl), because provision is what applies the retention policy.")]
    public void activity_container_bicep_enables_ttl_and_stamps_on_create_and_replace()
    {
        // Arrange
        var repoRoot = FindRepoRoot();
        var bicepPath = Path.Combine(repoRoot, "Infrastructure", "cosmos-db.bicep");
        var bicep = File.ReadAllText(bicepPath);

        var activitySectionStart = bicep.IndexOf("name: 'Activity'", StringComparison.Ordinal);
        activitySectionStart.Should().BeGreaterThanOrEqualTo(0);
        var activitySection = bicep[activitySectionStart..];

        // Act
        var hasDefaultTtl = activitySection.Contains("defaultTtl: -1", StringComparison.Ordinal);
        var hasInitiateConstant = bicep.Contains($"initiateTtlSeconds = {ActivityBookingTtl.InitiateSeconds}",
            StringComparison.Ordinal);
        var hasCompleteConstant = bicep.Contains($"completeTtlSeconds = {ActivityBookingTtl.CompleteSeconds}",
            StringComparison.Ordinal);
        var stampsInitiateOnCreate =
            bicep.Contains("'ttl': initiateTtlSeconds", StringComparison.Ordinal) ||
            bicep.Contains("\"ttl\": initiateTtlSeconds", StringComparison.Ordinal) ||
            bicep.Contains("ttl: initiateTtlSeconds", StringComparison.Ordinal);
        var stampsCompleteOnReplace = bicep.Contains("doc.ttl = completeTtlSeconds", StringComparison.Ordinal);

        // Assert
        hasDefaultTtl.Should().BeTrue("Activity container must set defaultTtl: -1 for opt-in item TTL");
        hasInitiateConstant.Should().BeTrue(
            "bicep bookActivity must assign initiateTtlSeconds = {0}", ActivityBookingTtl.InitiateSeconds);
        hasCompleteConstant.Should().BeTrue(
            "bicep bookActivity must assign completeTtlSeconds = {0}", ActivityBookingTtl.CompleteSeconds);
        stampsInitiateOnCreate.Should().BeTrue(
            "bicep bookActivity createDocument path must stamp ttl from initiateTtlSeconds");
        stampsCompleteOnReplace.Should().BeTrue(
            "bicep bookActivity replaceDocument path must assign doc.ttl = completeTtlSeconds");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RedditPodcastPoster.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate RedditPodcastPoster.slnx from test base directory.");
    }
}
