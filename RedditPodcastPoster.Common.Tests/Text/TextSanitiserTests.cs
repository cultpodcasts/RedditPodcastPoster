using System.Collections.Generic;
using System.Text.RegularExpressions;
using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Common.KnownTerms;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;
using Xunit;

namespace RedditPodcastPoster.Common.Tests.Text;

public class TextSanitiserTests
{
    private readonly AutoMocker _mocker;

    public TextSanitiserTests()
    {
        _mocker = new AutoMocker();
        _mocker.GetMock<IKnownTermsProvider>().Setup(x => x.GetKnownTerms()).Returns(new KnownTerms.KnownTerms());
    }

    private TextSanitiser Sut => _mocker.CreateInstance<TextSanitiser>();

    [Fact]
    public void Sanitise_PlainText_IsCorrect()
    {
        // arrange
        var text =
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Suspendisse tempus laoreet felis, varius cursus tortor varius ac. Nunc molestie velit est, non ultricies nisi luctus scelerisque. Nunc ante felis, pharetra in nisl eu, vulputate ornare elit. Nullam mi tortor, euismod vitae cursus eget, aliquet a magna. Ut consequat velit sodales varius tincidunt. Phasellus non nulla tellus. Nulla tempus euismod commodo. Pellentesque vitae mi convallis, cursus turpis eu, laoreet nunc. Mauris id nulla lacinia, tristique dui sit amet, sollicitudin erat. Nullam pulvinar metus risus, ac rutrum arcu aliquam ac. Interdum et malesuada fames ac ante ipsum primis in faucibus. Curabitur tempus velit luctus libero venenatis, eget imperdiet nisi bibendum. Cras aliquet augue sit amet lacus bibendum dapibus. Etiam ac quam sit amet sapien ultricies pretium eu ac ligula.\r\n\r\nSuspendisse volutpat felis eu nisi tempus pulvinar. Duis sit amet magna ut turpis vulputate sodales. Maecenas fringilla libero placerat, egestas ante a, dictum sapien. Aenean mi augue, luctus eu euismod ac, aliquam sit amet justo. Donec ut mauris eu turpis faucibus volutpat. Duis pellentesque mollis massa. Etiam cursus velit enim. Maecenas sagittis ultricies mauris vel iaculis. Suspendisse ultrices volutpat metus. Duis et varius erat. Pellentesque sed placerat sem. Aliquam ullamcorper neque laoreet, lacinia lorem nec, posuere ipsum.\r\n\r\nNulla venenatis nibh nec nisl sodales aliquam. Ut hendrerit hendrerit magna, vitae vulputate diam pulvinar id. Praesent congue sodales elit, vitae elementum est auctor at. Nam in lacinia ipsum, quis sodales massa. Cras ullamcorper id sem vitae sagittis. Aliquam ullamcorper dignissim aliquet. Curabitur eleifend massa eu sem facilisis, ut porttitor odio mattis. Aenean sed purus id erat tempor volutpat. Nulla sed nunc lacus. Phasellus vel lectus maximus, fringilla justo sed, suscipit erat. Nam laoreet, mi non feugiat auctor, mauris quam suscipit elit, non luctus erat elit ac velit. Pellentesque nec pretium nisi. Integer bibendum tortor convallis tellus pharetra accumsan. Maecenas pretium blandit velit, et ultricies lectus fermentum ac. Nullam lorem orci, vulputate id interdum eu, finibus sed nisi.\r\n\r\nNulla eleifend eros sed consectetur volutpat. Aenean semper posuere odio gravida varius. Sed in urna tincidunt, pretium sapien vel, volutpat urna. Praesent quam massa, lobortis ut est quis, accumsan rutrum felis. Sed finibus odio quis facilisis vestibulum. Morbi venenatis mi ut eros cursus, a pellentesque ligula facilisis. Aenean aliquet efficitur metus, eu aliquet lorem condimentum laoreet. Nunc ligula orci, interdum vitae pretium sit amet, congue in nulla. Phasellus vel consectetur augue, non cursus felis.\r\n\r\nCurabitur scelerisque libero vitae eros feugiat fermentum. Aliquam ut quam ac massa posuere lobortis accumsan ut tellus. Suspendisse ultricies eros felis, eu luctus metus tincidunt at. Suspendisse mi augue, hendrerit non tempus vel, vestibulum non velit. Maecenas consectetur tincidunt lorem eu aliquam. Etiam quis lacus aliquet, fermentum ex placerat, ornare mi. Nam pellentesque porta tincidunt. Phasellus vitae mi rhoncus, pellentesque nunc sit amet, lobortis turpis. ";
        // act
        var result = Sut.Sanitise(text);
        // assert
        result.Should().Be(text.TrimEnd());
    }

    [Fact]
    public void Sanitise_SimpleHtml_IsCorrect()
    {
        // arrange
        var text =
            "<b>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</b> Suspendisse tempus laoreet felis, varius cursus tortor varius ac. Nunc molestie velit est";
        // act
        var result = Sut.Sanitise(text);
        // assert
        result.Should()
            .Be(
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Suspendisse tempus laoreet felis, varius cursus tortor varius ac. Nunc molestie velit est");
    }

    [Fact]
    public void Sanitise_MalformedHtml_IsCorrect()
    {
        // arrange
        var text =
            "<b>Lorem ipsum dolor sit amet, consectetur adipiscing elit.<strong> Suspendisse tempus laoreet felis, varius cursus tortor varius ac. Nunc molestie velit est";
        // act
        var result = Sut.Sanitise(text);
        // assert
        result.Should()
            .Be(
                "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Suspendisse tempus laoreet felis, varius cursus tortor varius ac. Nunc molestie velit est");
    }

    [Theory]
    [InlineData("Ep. 263 Odyssey Study Group Revisited Part 3", "Odyssey Study Group Revisited Pt.3")]
    [InlineData("Odyssey Study Group Revisited Part 3", "Odyssey Study Group Revisited Pt.3")]
    [InlineData("Ep. 263 Odyssey Study Group Revisited", "Odyssey Study Group Revisited")]
    [InlineData("Odyssey Study Group Revisited", "Odyssey Study Group Revisited")]
    public void ExtractTitle_WithCultVaultPattern_IsCorrect(string content, string expected)
    {
        // arrange
        var regex = @"(?'episodenumberprefix'^Ep\.? \d+ )?(?'title'.*?)(?'partsection' Part (?'partnumber'\d+))?$";
        // act
        var result = Sut.ExtractTitle(content, new Regex(regex));
        // assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Today's episode is sponsored by BetterHelp. Last week", "Last week")]
    public void ExtractBody_WithALittleBitCultyPattern_IsCorrect(string content, string expected)
    {
        // arrange
        var regex = @"^Today's episode is sponsored by BetterHelp. (?'body'.*)$";
        // act
        var result = Sut.ExtractBody(content, new Regex(regex));
        // assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Understanding Extremist Authoritarian Aects - w/Christian Szurko")]
    [InlineData("The Start of the Sentence")]
    public void SanitiseTitle_WithKnownTerm_MaintainsTerm(string expected)
    {
        // arrange
        (Podcast, IEnumerable<Episode>) podcastEpisode = (new Podcast(), new[] {new Episode {Title = expected}});
        // act
        var result = Sut.SanitiseTitle(podcastEpisode.ToPostModel());
        // assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("I Was #Fairgamed! And I Love It!!! ;)", "I Was Fairgamed! And I Love It!!! ;)")]
    [InlineData("25 How To Handle Trauma! Ex Cult Member Explains ",
        "25 How To Handle Trauma! Ex Cult Member Explains")]
    public void SanitiseBody_WithKnownTerm_RemovesHashTags(string input, string expected)
    {
        // arrange
        (Podcast, IEnumerable<Episode>) podcastEpisode = (new Podcast(), new[] {new Episode {Title = input}});
        // act
        var result = Sut.SanitiseTitle(podcastEpisode.ToPostModel());
        // assert
        result.Should().Be(expected);
    }
}