using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class CatalogueFeedRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A catalogue count returns the first populated page and does not read later pages, because Cosmos COUNT(1) is a single value and an empty page is not that value.")]
    public async Task count_returns_the_first_populated_page()
    {
        // Arrange
        var count = _fixture.Create<int>();
        var pages = new ScriptedIterator<int>([[], [count], [_fixture.Create<int>()]]);

        // Act
        var result = await CatalogueFeed.Count(pages, _ => { });

        // Assert
        result.Should().Be(count);
        pages.Reads.Should().Be(2);
    }

    [Fact(DisplayName =
        "A catalogue count is zero when Cosmos returns no pages, because an empty container has nothing to count.")]
    public async Task count_is_zero_when_there_are_no_pages()
    {
        // Arrange
        var pages = new ScriptedIterator<int>([]);

        // Act
        var result = await CatalogueFeed.Count(pages, _ => { });

        // Assert
        result.Should().Be(0);
    }

    [Fact(DisplayName =
        "A failed catalogue count is logged and rethrown, because a partial Cosmos failure must not be reported as zero items.")]
    public async Task count_logs_and_rethrows_a_failed_page()
    {
        // Arrange
        var failure = new InvalidOperationException(_fixture.Create<string>());
        var pages = new ScriptedIterator<int>(failure);
        Exception? logged = null;

        // Act
        var act = () => CatalogueFeed.Count(pages, ex => logged = ex);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(failure.Message);
        logged.Should().BeSameAs(failure);
    }

    [Fact(DisplayName =
        "A catalogue feed yields every item from every page in order, because callers page through the whole container.")]
    public async Task read_yields_every_page_in_order()
    {
        // Arrange
        var first = _fixture.Create<string>();
        var second = _fixture.Create<string>();
        var third = _fixture.Create<string>();
        var pages = new ScriptedIterator<string>([[first], [second, third]]);

        // Act
        var items = new List<string>();
        await foreach (var item in CatalogueFeed.Read<string>(pages, _ => { }))
        {
            items.Add(item);
        }

        // Assert
        items.Should().Equal(first, second, third);
    }

    [Fact(DisplayName =
        "A failed catalogue page is logged and rethrown after earlier pages have been yielded, because those items were already read and the failure must not become an empty result.")]
    public async Task read_yields_successful_pages_then_rethrows()
    {
        // Arrange
        var kept = _fixture.Create<string>();
        var failure = new InvalidOperationException(_fixture.Create<string>());
        var pages = new ScriptedIterator<string>([[kept]], failure);
        Exception? logged = null;
        var items = new List<string>();

        // Act
        var act = async () =>
        {
            await foreach (var item in CatalogueFeed.Read<string>(pages, ex => logged = ex))
            {
                items.Add(item);
            }
        };

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(failure.Message);
        items.Should().Equal(kept);
        logged.Should().BeSameAs(failure);
    }

    private sealed class ScriptedIterator<T> : FeedIterator<T>
    {
        private readonly Queue<IReadOnlyList<T>> _pages = new();
        private readonly Exception? _terminalFailure;
        private bool _failed;

        public ScriptedIterator(IReadOnlyList<IReadOnlyList<T>> pages, Exception? terminalFailure = null)
        {
            foreach (var page in pages)
            {
                _pages.Enqueue(page);
            }

            _terminalFailure = terminalFailure;
        }

        public ScriptedIterator(Exception failure)
        {
            _terminalFailure = failure;
        }

        public int Reads { get; private set; }

        public override bool HasMoreResults => _pages.Count > 0 || (_terminalFailure != null && !_failed);

        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            if (_pages.Count > 0)
            {
                return Task.FromResult<FeedResponse<T>>(new Page(_pages.Dequeue()));
            }

            _failed = true;
            throw _terminalFailure ?? new InvalidOperationException("No further catalogue page was scripted.");
        }

        private sealed class Page(IReadOnlyList<T> items) : FeedResponse<T>
        {
            public override string ContinuationToken => string.Empty;

            public override int Count => items.Count;

            public override string IndexMetrics => string.Empty;

            public override Headers Headers => new();

            public override IEnumerable<T> Resource => items;

            public override HttpStatusCode StatusCode => HttpStatusCode.OK;

            public override CosmosDiagnostics Diagnostics => null!;

            public override IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        }
    }
}
