using D1SearchReconcile;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ConsoleApps.Composition.Tests;

public class D1SearchReconcileLoggingRules
{
    [Fact(DisplayName =
        "While the progress display holds the log, D1SearchReconcile queues log lines and writes them in order only " +
        "when the hold is released, because a line written under a live Spectre display is drawn over and garbled " +
        "(#1475).")]
    public void held_lines_are_written_after_release_in_order()
    {
        // Arrange
        var output = new StringWriter();
        using var provider = new DeferredConsoleLoggerProvider(output);
        var logger = provider.CreateLogger("Reconciler");
        var hold = provider.Hold();

        // Act
        logger.LogWarning("first");
        logger.LogError("second");
        var duringHold = output.ToString();
        hold.Dispose();
        logger.LogWarning("third");

        // Assert
        duringHold.Should().BeEmpty();
        var text = output.ToString();
        text.Should().Contain("warn: Reconciler[0]").And.Contain("fail: Reconciler[0]");
        text.IndexOf("first", StringComparison.Ordinal).Should()
            .BeLessThan(text.IndexOf("second", StringComparison.Ordinal));
        text.IndexOf("second", StringComparison.Ordinal).Should()
            .BeLessThan(text.IndexOf("third", StringComparison.Ordinal));
    }

    [Fact(DisplayName =
        "With nested holds, D1SearchReconcile writes nothing until the last hold is released, and releasing a hold " +
        "twice does not release another, because the display may be held by more than one caller.")]
    public void nested_holds_flush_only_when_the_last_is_released()
    {
        // Arrange
        var output = new StringWriter();
        using var provider = new DeferredConsoleLoggerProvider(output);
        var logger = provider.CreateLogger("Reconciler");
        var outer = provider.Hold();
        var inner = provider.Hold();

        // Act
        logger.LogWarning("held");
        inner.Dispose();
        inner.Dispose();
        var afterInner = output.ToString();
        outer.Dispose();

        // Assert
        afterInner.Should().BeEmpty();
        output.ToString().Should().Contain("held");
    }
}