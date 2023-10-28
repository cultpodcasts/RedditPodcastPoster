using System.Xml.Linq;
using FluentAssertions;
using Moq.AutoMock;

namespace RedditPodcastPoster.YouTubePushNotifications.Tests;

public class NotificationAdaptorTests
{
    private const string KnownNotificationMessage =
        @"<?xml version='1.0' encoding='UTF-8'?> <feed xmlns:yt=""http://www.youtube.com/xml/schemas/2015"" xmlns:media=""http://search.yahoo.com/mrss/"" xmlns=""http://www.w3.org/2005/Atom""><link rel=""self"" href=""http://www.youtube.com/feeds/videos.xml?channel_id=UCsT0YIqwnpJCM-mx7-gSA4Q""/><id>yt:channel:sT0YIqwnpJCM-mx7-gSA4Q</id><yt:channelId>sT0YIqwnpJCM-mx7-gSA4Q</yt:channelId><title>TEDx Talks</title><link rel=""alternate"" href=""https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q""/><author> <name>TEDx Talks</name> <uri>https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q</uri> </author><published>2009-06-23T16:00:48+00:00</published><entry> <id>yt:video:SdcFlRBp-Vc</id> <yt:videoId>SdcFlRBp-Vc</yt:videoId> <yt:channelId>UCsT0YIqwnpJCM-mx7-gSA4Q</yt:channelId> <title>Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=SdcFlRBp-Vc""/> <author> <name>TEDx Talks</name> <uri>https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q</uri> </author> <published>2023-10-28T14:00:17+00:00</published> <updated>2023-10-28T18:45:32+00:00</updated> <media:group> <media:title>Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool</media:title> <media:content url=""https://www.youtube.com/v/SdcFlRBp-Vc?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i4.ytimg.com/vi/SdcFlRBp-Vc/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>Through her engaging storytelling, Karen takes us on a transformative journey, exploring the pivotal moments in her own life where she confronted the daunting question, &quot;What is my dream job?&quot; From her initial struggles as an engineering graduate to her remarkable triple career change, she unveils the crucial questions we must ask ourselves to shape our own game plan. Karen emphasizes the significance of understanding our strengths, goals, and gaps while highlighting the importance of perseverance and adaptation. Karen has had significant years of experience in the Luxury, Consumer Goods and Higher Education sectors. She graduated from an Industrial Engineering degree and like most people, especially today, she has encountered various changes in her career in life including moving multiple job functions and different countries with different languages. She has navigated different kinds of changes that we all encounter with her consistently positive attitude that stirs enthusiasm and commitment with everyone around her. Today, her new-found purpose is to help companies meet great talent and help talented candidates find fulfilling work in companies that they would love in the Human Resources field. Karen's proudest achievement to-date is having the most fulfilling roles in the world -- being a wife and a mother whose children are learning 4 languages at the same time. This talk was given at a TEDx event using the TED conference format but independently organized by a local community. Learn more at https://www.ted.com/tedx</media:description> <media:community> <media:starRating count=""598"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""11808""/> </media:community> </media:group> </entry></feed>";

    private readonly AutoMocker _mocker;

    public NotificationAdaptorTests()
    {
        _mocker = new AutoMocker();
    }

    private INotificationAdaptor Sut => _mocker.CreateInstance<NotificationAdaptor>();

    [Fact]
    public void Adapt_WithKnownMessage_IsCorrect()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Id.Should().Be("yt:channel:sT0YIqwnpJCM-mx7-gSA4Q");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsChannelId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.ChannelId.Should().Be("sT0YIqwnpJCM-mx7-gSA4Q");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsTitle()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Title.Should().Be("TEDx Talks");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsLinkAlternative()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.LinkAlternative.Should()
            .Be(new Uri("https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q", UriKind.Absolute));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsAuthorName()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.AuthorName.Should().Be("TEDx Talks");
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsAuthorUri()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.AuthorUri.Should()
            .Be(new Uri("https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q", UriKind.Absolute));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsPublished()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Published.Should().Be(DateTime.Parse("2009-06-23T16:00:48+00:00"));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntry()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Should().NotBeNull();
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Id.Should().Be("yt:video:SdcFlRBp-Vc");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryVideoId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.VideoId.Should().Be("SdcFlRBp-Vc");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryChannelId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.ChannelId.Should().Be("UCsT0YIqwnpJCM-mx7-gSA4Q");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryTitle()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Title.Should().Be("Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryLinkAlternative()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.LinkAlternative.Should()
            .Be(new Uri("https://www.youtube.com/watch?v=SdcFlRBp-Vc", UriKind.Absolute));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryAuthorName()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.AuthorName.Should().Be("TEDx Talks");
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryAuthorUri()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.AuthorUri.Should()
            .Be(new Uri("https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q", UriKind.Absolute));
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryPublished()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Published.Should().Be(DateTime.Parse("2023-10-28T14:00:17+00:00"));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryUpdated()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Updated.Should().Be(DateTime.Parse("2023-10-28T18:45:32+00:00"));
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroup()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Should().NotBeNull();
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupTitle()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Title.Should()
            .Be("Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupContentUrl()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.ContentUrl.Should()
            .Be(new Uri("https://www.youtube.com/v/SdcFlRBp-Vc?version=3", UriKind.Absolute));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupContentWidth()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.ContentWidth.Should().Be(640);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupContentHeight()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.ContentHeight.Should().Be(390);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupThumbnailUrl()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.ThumbnailUrl.Should()
            .Be(new Uri("https://i4.ytimg.com/vi/SdcFlRBp-Vc/hqdefault.jpg", UriKind.Absolute));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupThumbnailWidth()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.ThumbnailWidth.Should().Be(480);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupThumbnailHeight()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.ThumbnailHeight.Should().Be(360);
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupDescription()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Description.Should().StartWith("Through her engaging storytelling,");
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunity()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Community.Should().NotBeNull();
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingCount()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Community.StarRatingCount.Should().Be(598);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingAverage()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Community.StarRatingAverage.Should().Be(5.00m);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingMin()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Community.StarRatingMin.Should().Be(1);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingMax()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Community.StarRatingMax.Should().Be(5);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStatisticsViews()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entry.Group.Community.StatisticsViews.Should().Be(11808);
    }
}