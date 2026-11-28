using Azure;
using FluentAssertions;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class ActivityBookingTtlRules
{
    [Fact(DisplayName =
        "Activity booking keeps the Cosmos initiate/complete ledger and expires rows via item TTL " +
        "(initiate 2 days, complete 7 days), because Durable alone does not replace the cross-instance " +
        "lock and completed docs must not accumulate forever (#1302).")]
    public void activity_booking_ttl_policy_keeps_ledger_with_bounded_retention()
    {
        // Arrange
        const int twoDays = 2 * 24 * 60 * 60;
        const int sevenDays = 7 * 24 * 60 * 60;

        // Act
        var initiateSeconds = ActivityBookingTtl.InitiateSeconds;
        var completeSeconds = ActivityBookingTtl.CompleteSeconds;

        // Assert
        initiateSeconds.Should().Be(twoDays);
        completeSeconds.Should().Be(sevenDays);
        completeSeconds.Should().BeGreaterThan(initiateSeconds);
    }

    [Fact(DisplayName =
        "The bookActivity JS reference copy stamps the same initiate and complete TTL seconds as " +
        "ActivityBookingTtl, because the stored procedure is what writes ttl on create/replace.")]
    public void book_activity_js_matches_activity_booking_ttl_constants()
    {
        // Arrange
        var repoRoot = FindRepoRoot();
        var jsPath = Path.Combine(repoRoot, "Azure-Resources", "CosmosDb", "bookActivity.js");
        var js = File.ReadAllText(jsPath);

        // Act
        var hasInitiate = js.Contains($"initiateTtlSeconds = {ActivityBookingTtl.InitiateSeconds}",
            StringComparison.Ordinal);
        var hasComplete = js.Contains($"completeTtlSeconds = {ActivityBookingTtl.CompleteSeconds}",
            StringComparison.Ordinal);

        // Assert
        hasInitiate.Should().BeTrue(
            "bookActivity.js must assign initiateTtlSeconds = {0}", ActivityBookingTtl.InitiateSeconds);
        hasComplete.Should().BeTrue(
            "bookActivity.js must assign completeTtlSeconds = {0}", ActivityBookingTtl.CompleteSeconds);
    }

    [Fact(DisplayName =
        "The Activity container bicep enables opt-in TTL (defaultTtl -1) and the embedded bookActivity " +
        "sproc uses the same TTL seconds as ActivityBookingTtl, because provision is what applies the policy.")]
    public void activity_container_bicep_enables_ttl_and_matches_booking_constants()
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
        var hasInitiate = bicep.Contains($"initiateTtlSeconds = {ActivityBookingTtl.InitiateSeconds}",
            StringComparison.Ordinal);
        var hasComplete = bicep.Contains($"completeTtlSeconds = {ActivityBookingTtl.CompleteSeconds}",
            StringComparison.Ordinal);

        // Assert
        hasDefaultTtl.Should().BeTrue("Activity container must set defaultTtl: -1 for opt-in item TTL");
        hasInitiate.Should().BeTrue(
            "bicep bookActivity must assign initiateTtlSeconds = {0}", ActivityBookingTtl.InitiateSeconds);
        hasComplete.Should().BeTrue(
            "bicep bookActivity must assign completeTtlSeconds = {0}", ActivityBookingTtl.CompleteSeconds);
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
