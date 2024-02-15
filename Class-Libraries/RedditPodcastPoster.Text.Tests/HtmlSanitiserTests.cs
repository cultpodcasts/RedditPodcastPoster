using FluentAssertions;
using Moq.AutoMock;

namespace RedditPodcastPoster.Text.Tests;

public class HtmlSanitiserTests
{
    private readonly AutoMocker _mocker = new();

    private IHtmlSanitiser Sut => _mocker.CreateInstance<HtmlSanitiser>();

    [Fact]
    public void GetDescription_IsCorrect()
    {
        // arrange
        var html = "text";
        // act
        var result = Sut.Sanitise(html);
        // assert
        result.Should().Be("text");
    }

    [Fact]
    public void GetDescriptionWithParagraphs_IsCorrect()
    {
        // arrange
        var html = "<p>Para 1<p><p>Para 2</p>";
        // act
        var result = Sut.Sanitise(html);
        // assert
        result.Should().Be("Para 1 Para 2");
    }

    [Fact]
    public void GetDescriptionWithBreaks_IsCorrect()
    {
        // arrange
        var html = "Para 1<br />Para 2";
        // act
        var result = Sut.Sanitise(html);
        // assert
        result.Should().Be("Para 1 Para 2");
    }

    [Fact]
    public void GetDescriptionWithParagraphsAndBreaks_IsCorrect()
    {
        // arrange
        var html = "<p>Para 1<br />Para 2<p><p>Para 3</p>";
        // act
        var result = Sut.Sanitise(html);
        // assert
        result.Should().Be("Para 1 Para 2 Para 3");
    }
}