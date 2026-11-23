using System.Text.RegularExpressions;
using CreateSearchIndex;
using FluentAssertions;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;

namespace CreateSearchIndex.Tests;

/// <summary>
/// #1527: a missing ')' on CONCAT in <see cref="PlayableSearchSql.YouTubeAlternatesProjection"/>
/// made every indexer data-source query invalid (Azure SC1001 near FROM). These tests keep the
/// projection, every sibling query, and the episode pull SQL balanced and well-formed.
/// </summary>
public class YouTubeAlternatesCosmosSqlRules
{
    /// <summary>
    /// Exact pre-fix body of <see cref="PlayableSearchSql.YouTubeAlternatesProjection"/> from
    /// RPP #1524 (cfca4452): CONCAT is never closed before <c>FROM</c>.
    /// </summary>
    private static string BrokenYouTubeAlternatesProjection(string alias) =>
        $"IIF(IS_DEFINED({alias}.additionalYouTube) AND IS_ARRAY({alias}.additionalYouTube) AND ARRAY_LENGTH({alias}.additionalYouTube) > 0, " +
        "ARRAY(SELECT VALUE CONCAT(" +
        "a.youTubeId, \"|\", " +
        "IIF(IS_DEFINED(a.release) AND IS_STRING(a.release) AND LENGTH(a.release) >= 10, SUBSTRING(a.release, 0, 10), \"\"), " +
        "\"|\", " +
        "IIF(IS_DEFINED(a.channelName) AND IS_STRING(a.channelName), a.channelName, \"\") " +
        $"FROM a IN {alias}.additionalYouTube), null)";

    public static TheoryData<string, string> CurrentPullSql()
    {
        var keys = StreamingServiceCatalog.SearchEncodedKeys;
        var images = StreamingServiceCatalog.ImageCoalesceOrder;
        return new TheoryData<string, string>
        {
            { "YouTubeAlternatesProjection(e)", PlayableSearchSql.YouTubeAlternatesProjection("e") },
            { "YouTubeAlternatesProjection(c)", PlayableSearchSql.YouTubeAlternatesProjection("c") },
            {
                "SiblingQuery(film)",
                PlayableSearchSql.SiblingQuery(
                    new PlayableSearchSql.SiblingProjection(
                        SearchContentKind.Film, "c.name", null, IncludeSeries: false),
                    keys,
                    images)
            },
            {
                "SiblingQuery(tvShowEpisode)",
                PlayableSearchSql.SiblingQuery(
                    new PlayableSearchSql.SiblingProjection(
                        SearchContentKind.TvShowEpisode, "c.title", "c.tvShowName", IncludeSeries: true),
                    keys,
                    images)
            },
            {
                "SiblingQuery(newsReport)",
                PlayableSearchSql.SiblingQuery(
                    new PlayableSearchSql.SiblingProjection(
                        SearchContentKind.NewsReport, "c.title", "c.newsOrganisationName", IncludeSeries: true),
                    keys,
                    images)
            },
            { "EpisodeDataSourceQuery", CreateSearchIndexProcessor.EpisodeDataSourceQuery() }
        };
    }

    [Fact(DisplayName =
        "The pre-fix youTubeAlternates projection (RPP #1524) has unbalanced parentheses and " +
        "CONCAT is still open at FROM, because that missing close is the SC1001 syntax error " +
        "that failed CreateSearchIndex --all-playables (#1527).")]
    public void pre_fix_projection_is_unbalanced_and_concat_is_open_at_from()
    {
        // Arrange
        var sql = BrokenYouTubeAlternatesProjection("e");

        // Act
        var balanced = CosmosSqlSyntax.ParenthesesAreBalanced(sql, out var balanceError);
        var wellFormed = CosmosSqlSyntax.HasWellFormedYouTubeAlternatesArray(sql, "e", out var shapeError);

        // Assert
        balanced.Should().BeFalse(
            "the #1527 fixture must stay unbalanced so this test fails if the checker is weakened: {0}",
            balanceError);
        wellFormed.Should().BeFalse(
            "CONCAT must still be open at FROM in the #1527 fixture: {0}",
            shapeError);
    }

    [Theory(DisplayName =
        "Each search pull query that embeds youTubeAlternates has balanced parentheses outside " +
        "string literals and a well-formed ARRAY(SELECT VALUE CONCAT(...) FROM a IN alias.additionalYouTube) " +
        "shape, because a missing CONCAT close is an invalid Cosmos query that Azure reports as SC1001 near FROM (#1527).")]
    [MemberData(nameof(CurrentPullSql))]
    public void pull_sql_is_balanced_and_youtube_alternates_array_is_well_formed(string name, string sql)
    {
        // Arrange
        var alias = name.Contains("(e)", StringComparison.Ordinal) || name == "EpisodeDataSourceQuery"
            ? "e"
            : "c";

        // Act
        var balanced = CosmosSqlSyntax.ParenthesesAreBalanced(sql, out var balanceError);
        var wellFormed = CosmosSqlSyntax.HasWellFormedYouTubeAlternatesArray(sql, alias, out var shapeError);

        // Assert
        balanced.Should().BeTrue("{0} must have balanced parentheses: {1}\n{2}", name, balanceError, sql);
        wellFormed.Should().BeTrue("{0} must close CONCAT before FROM a IN {1}.additionalYouTube: {2}\n{3}",
            name, alias, shapeError, sql);
    }
}

/// <summary>
/// Lightweight Cosmos SQL checks used because this repo has no public Cosmos SQL parser.
/// Punctuation inside single- or double-quoted string literals is ignored.
/// </summary>
internal static class CosmosSqlSyntax
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static bool ParenthesesAreBalanced(string sql, out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        if (!TryWalk(sql, 0, sql.Length, stopAtZero: false, out var depth, out _, out error))
        {
            return false;
        }

        if (depth != 0)
        {
            error = $"unbalanced parentheses: net depth {depth}";
            return false;
        }

        error = null;
        return true;
    }

    public static bool HasWellFormedYouTubeAlternatesArray(string sql, string alias, out string? error)
    {
        ArgumentException.ThrowIfNullOrEmpty(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        const string start = "ARRAY(SELECT VALUE CONCAT(";
        var index = sql.IndexOf(start, StringComparison.Ordinal);
        if (index < 0)
        {
            error = $"did not find {start}";
            return false;
        }

        var concatOpen = index + start.Length - 1;
        if (!TryWalk(sql, concatOpen, sql.Length, stopAtZero: true, out _, out var concatClose, out error))
        {
            return false;
        }

        var remainder = Whitespace.Replace(sql[(concatClose + 1)..], " ").TrimStart();
        var expected = $"FROM a IN {alias}.additionalYouTube)";
        if (!remainder.StartsWith(expected, StringComparison.Ordinal))
        {
            var previewLength = Math.Min(80, remainder.Length);
            error =
                $"expected CONCAT(...) to be followed by '{expected}', but found '{remainder[..previewLength]}'";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Walks <paramref name="sql"/> from <paramref name="startInclusive"/> to
    /// <paramref name="endExclusive"/>. When <paramref name="stopAtZero"/> is true, the start
    /// character must be '(' and the walk stops at its matching close (returned in
    /// <paramref name="stopIndex"/>). Otherwise the whole slice is scanned and
    /// <paramref name="stopIndex"/> is unused.
    /// </summary>
    private static bool TryWalk(
        string sql,
        int startInclusive,
        int endExclusive,
        bool stopAtZero,
        out int depth,
        out int stopIndex,
        out string? error)
    {
        depth = 0;
        stopIndex = -1;
        var inDouble = false;
        var inSingle = false;
        for (var i = startInclusive; i < endExclusive; i++)
        {
            var ch = sql[i];
            if (inDouble)
            {
                if (ch == '\\' && i + 1 < endExclusive)
                {
                    i++;
                    continue;
                }

                if (ch == '"')
                {
                    inDouble = false;
                }

                continue;
            }

            if (inSingle)
            {
                if (ch == '\\' && i + 1 < endExclusive)
                {
                    i++;
                    continue;
                }

                if (ch == '\'')
                {
                    inSingle = false;
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inDouble = true;
                    break;
                case '\'':
                    inSingle = true;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    if (depth < 0)
                    {
                        error = "a closing parenthesis has no matching opener";
                        return false;
                    }

                    if (stopAtZero && depth == 0)
                    {
                        stopIndex = i;
                        error = null;
                        return true;
                    }

                    break;
            }
        }

        if (inDouble || inSingle)
        {
            error = inDouble ? "unclosed double-quoted string" : "unclosed single-quoted string";
            return false;
        }

        if (stopAtZero)
        {
            error = "no matching close parenthesis";
            return false;
        }

        error = null;
        return true;
    }
}
