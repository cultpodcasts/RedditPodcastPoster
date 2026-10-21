using FluentAssertions;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.EpisodeLookup.BusinessRules.Tests.BusinessRules;

/// <summary>Use-case entry points mark the shared context in place, so circuit-breaker flags stay shared.</summary>
public class EpisodeLookupUseCasesRules
{
    public static TheoryData<string, EpisodeLookupFlow> EntryPoints() => new()
    {
        { nameof(EpisodeLookupUseCases.ForIndex), EpisodeLookupFlow.Index },
        { nameof(EpisodeLookupUseCases.ForSubmit), EpisodeLookupFlow.Submit },
        { nameof(EpisodeLookupUseCases.ForDiscovery), EpisodeLookupFlow.Discovery },
        { nameof(EpisodeLookupUseCases.ForEnrich), EpisodeLookupFlow.Enrich },
    };

    [Theory(DisplayName = "Each use-case entry point sets its flow on the same IndexingContext instance and returns it.")]
    [MemberData(nameof(EntryPoints))]
    public void Entry_point_marks_context_in_place(string entryPoint, EpisodeLookupFlow expected)
    {
        // Arrange
        var context = new IndexingContext { LookupFlow = EpisodeLookupFlow.Submit };
        Func<IndexingContext, IndexingContext> mark = entryPoint switch
        {
            nameof(EpisodeLookupUseCases.ForIndex) => EpisodeLookupUseCases.ForIndex,
            nameof(EpisodeLookupUseCases.ForDiscovery) => EpisodeLookupUseCases.ForDiscovery,
            nameof(EpisodeLookupUseCases.ForEnrich) => EpisodeLookupUseCases.ForEnrich,
            _ => EpisodeLookupUseCases.ForSubmit
        };

        // Act
        var result = mark(context);

        // Assert
        result.Should().BeSameAs(context);
        context.LookupFlow.Should().Be(expected);
    }

    [Fact(DisplayName = "An unmarked IndexingContext is the Submit use case.")]
    public void Default_is_submit()
    {
        // Arrange
        var context = new IndexingContext();

        // Act
        var flow = context.LookupFlow;

        // Assert
        flow.Should().Be(EpisodeLookupFlow.Submit);
    }
}
