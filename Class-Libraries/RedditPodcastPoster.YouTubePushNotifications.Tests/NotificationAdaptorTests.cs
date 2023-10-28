using System.Xml.Linq;
using FluentAssertions;
using Moq.AutoMock;

namespace RedditPodcastPoster.YouTubePushNotifications.Tests;

public class NotificationAdaptorTests
{
    private const string KnownNotificationMessage =
        @"<?xml version='1.0' encoding='UTF-8'?> <feed xmlns:yt=""http://www.youtube.com/xml/schemas/2015"" xmlns:media=""http://search.yahoo.com/mrss/"" xmlns=""http://www.w3.org/2005/Atom""><link rel=""self"" href=""http://www.youtube.com/feeds/videos.xml?channel_id=UCsT0YIqwnpJCM-mx7-gSA4Q""/><id>yt:channel:sT0YIqwnpJCM-mx7-gSA4Q</id><yt:channelId>sT0YIqwnpJCM-mx7-gSA4Q</yt:channelId><title>TEDx Talks</title><link rel=""alternate"" href=""https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q""/><author> <name>TEDx Talks</name> <uri>https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q</uri> </author><published>2009-06-23T16:00:48+00:00</published><entry> <id>yt:video:SdcFlRBp-Vc</id> <yt:videoId>SdcFlRBp-Vc</yt:videoId> <yt:channelId>UCsT0YIqwnpJCM-mx7-gSA4Q</yt:channelId> <title>Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=SdcFlRBp-Vc""/> <author> <name>TEDx Talks</name> <uri>https://www.youtube.com/channel/UCsT0YIqwnpJCM-mx7-gSA4Q</uri> </author> <published>2023-10-28T14:00:17+00:00</published> <updated>2023-10-28T18:45:32+00:00</updated> <media:group> <media:title>Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool</media:title> <media:content url=""https://www.youtube.com/v/SdcFlRBp-Vc?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i4.ytimg.com/vi/SdcFlRBp-Vc/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>Through her engaging storytelling, Karen takes us on a transformative journey, exploring the pivotal moments in her own life where she confronted the daunting question, &quot;What is my dream job?&quot; From her initial struggles as an engineering graduate to her remarkable triple career change, she unveils the crucial questions we must ask ourselves to shape our own game plan. Karen emphasizes the significance of understanding our strengths, goals, and gaps while highlighting the importance of perseverance and adaptation. Karen has had significant years of experience in the Luxury, Consumer Goods and Higher Education sectors. She graduated from an Industrial Engineering degree and like most people, especially today, she has encountered various changes in her career in life including moving multiple job functions and different countries with different languages. She has navigated different kinds of changes that we all encounter with her consistently positive attitude that stirs enthusiasm and commitment with everyone around her. Today, her new-found purpose is to help companies meet great talent and help talented candidates find fulfilling work in companies that they would love in the Human Resources field. Karen's proudest achievement to-date is having the most fulfilling roles in the world -- being a wife and a mother whose children are learning 4 languages at the same time. This talk was given at a TEDx event using the TED conference format but independently organized by a local community. Learn more at https://www.ted.com/tedx</media:description> <media:community> <media:starRating count=""598"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""11808""/> </media:community> </media:group> </entry></feed>";

    private const string KnownNotificationMessageMultipleEntries =
        @"<?xml version='1.0' encoding='UTF-8'?> <feed xmlns:yt=""http://www.youtube.com/xml/schemas/2015"" xmlns:media=""http://search.yahoo.com/mrss/"" xmlns=""http://www.w3.org/2005/Atom""><link rel=""self"" href=""http://www.youtube.com/feeds/videos.xml?channel_id=UC9r9HYFxEQOBXSopFS61ZWg""/><id>yt:channel:9r9HYFxEQOBXSopFS61ZWg</id><yt:channelId>9r9HYFxEQOBXSopFS61ZWg</yt:channelId><title>MeidasTouch</title><link rel=""alternate"" href=""https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg""/><author> <name>MeidasTouch</name> <uri>https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg</uri> </author><published>2010-05-13T23:02:14+00:00</published><entry> <id>yt:video:pzZwxmeez-Q</id> <yt:videoId>pzZwxmeez-Q</yt:videoId> <yt:channelId>UC9r9HYFxEQOBXSopFS61ZWg</yt:channelId> <title>Fox hosts left STUNNED, get NEWS they were DREADING live on air</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=pzZwxmeez-Q""/> <author> <name>MeidasTouch</name> <uri>https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg</uri> </author> <published>2023-10-28T22:30:04+00:00</published> <updated>2023-10-28T22:30:05+00:00</updated> <media:group> <media:title>Fox hosts left STUNNED, get NEWS they were DREADING live on air</media:title> <media:content url=""https://www.youtube.com/v/pzZwxmeez-Q?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i1.ytimg.com/vi/pzZwxmeez-Q/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>It's that time of the month when Fox is forced to announce positive news about the Biden economy. Francis Maxwell reports. Visit https://meidastouch.com for more! Support the MeidasTouch Network: https://patreon.com/meidastouch Add the MeidasTouch Podcast: https://podcasts.apple.com/us/podcast/the-meidastouch-podcast/id1510240831 Buy MeidasTouch Merch: https://store.meidastouch.com Follow MeidasTouch on Twitter: https://twitter.com/meidastouch Follow MeidasTouch on Facebook: https://facebook.com/meidastouch Follow MeidasTouch on Instagram: https://instagram.com/meidastouch Follow MeidasTouch on TikTok: https://tiktok.com/@meidastouch</media:description> <media:community> <media:starRating count=""102"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""665""/> </media:community> </media:group> </entry><entry> <id>yt:video:iH11HoLOxmw</id> <yt:videoId>iH11HoLOxmw</yt:videoId> <yt:channelId>UC9r9HYFxEQOBXSopFS61ZWg</yt:channelId> <title>Marjorie Taylor Greene gets the BAD NEWS she DESERVES</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=iH11HoLOxmw""/> <author> <name>MeidasTouch</name> <uri>https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg</uri> </author> <published>2023-10-28T21:30:06+00:00</published> <updated>2023-10-28T21:30:06+00:00</updated> <media:group> <media:title>Marjorie Taylor Greene gets the BAD NEWS she DESERVES</media:title> <media:content url=""https://www.youtube.com/v/iH11HoLOxmw?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i2.ytimg.com/vi/iH11HoLOxmw/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>Marjorie Taylor Greene continues to time and time again embarrass herself in front of the American people, and now this week pulling one of her most hypocritical acts yet. Gabe Sanchez breaks it down on a new ‘What was that?’ Get up to 40% off for a limited time when you go to https://shopbeam.com/GABE and use code GABE at checkout. Visit https://meidastouch.com for more! Support the MeidasTouch Network: https://patreon.com/meidastouch Add the MeidasTouch Podcast: https://podcasts.apple.com/us/podcast/the-meidastouch-podcast/id1510240831 Buy MeidasTouch Merch: https://store.meidastouch.com Follow MeidasTouch on Twitter: https://twitter.com/meidastouch Follow MeidasTouch on Facebook: https://facebook.com/meidastouch Follow MeidasTouch on Instagram: https://instagram.com/meidastouch Follow MeidasTouch on TikTok: https://tiktok.com/@meidastouch</media:description> <media:community> <media:starRating count=""9051"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""101628""/> </media:community> </media:group> </entry><entry> <id>yt:video:y5GKgSvaIig</id> <yt:videoId>y5GKgSvaIig</yt:videoId> <yt:channelId>UC9r9HYFxEQOBXSopFS61ZWg</yt:channelId> <title>CRY BABY Trump Kids get DEVASTATING NEWS, Trump BOXED IN</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=y5GKgSvaIig""/> <author> <name>MeidasTouch</name> <uri>https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg</uri> </author> <published>2023-10-28T20:00:25+00:00</published> <updated>2023-10-28T20:00:25+00:00</updated> <media:group> <media:title>CRY BABY Trump Kids get DEVASTATING NEWS, Trump BOXED IN</media:title> <media:content url=""https://www.youtube.com/v/y5GKgSvaIig?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i2.ytimg.com/vi/y5GKgSvaIig/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>New York Attorney General James and her gamble to try to crush the Trump Empire by having ALL of the Trump executive children — Don Jr, Eric, then Ivanka in that order testify BEFORE Donald Trump takes the stand 1 day before Election Day — will likely pay tremendous dividends in the NY fraud case. Michael Popok of Legal AF provides commentary on why the Attorney General sequenced this witness testimony in this particular order and what she’s trying to accomplish by having Trump go last. Visit https://meidastouch.com for more! Support the MeidasTouch Network: https://patreon.com/meidastouch Add the MeidasTouch Podcast: https://podcasts.apple.com/us/podcast/the-meidastouch-podcast/id1510240831 Buy MeidasTouch Merch: https://store.meidastouch.com Follow MeidasTouch on Twitter: https://twitter.com/meidastouch Follow MeidasTouch on Facebook: https://facebook.com/meidastouch Follow MeidasTouch on Instagram: https://instagram.com/meidastouch Follow MeidasTouch on TikTok: https://tiktok.com/@meidastouch</media:description> <media:community> <media:starRating count=""23049"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""242232""/> </media:community> </media:group> </entry><entry> <id>yt:video:Qf-uQ-toQR8</id> <yt:videoId>Qf-uQ-toQR8</yt:videoId> <yt:channelId>UC9r9HYFxEQOBXSopFS61ZWg</yt:channelId> <title>LIVE: Trump has BIGGEST MELTDOWN in Court, NOW MUST TESTIFY | Legal AF</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=Qf-uQ-toQR8""/> <author> <name>MeidasTouch</name> <uri>https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg</uri> </author> <published>2023-10-28T19:45:03+00:00</published> <updated>2023-10-28T19:48:35+00:00</updated> <media:group> <media:title>LIVE: Trump has BIGGEST MELTDOWN in Court, NOW MUST TESTIFY | Legal AF</media:title> <media:content url=""https://www.youtube.com/v/Qf-uQ-toQR8?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i2.ytimg.com/vi/Qf-uQ-toQR8/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>Ben Meiselas and Michael Popok are back with a new episode of the weekend edition of LegalAF. On this episode, they discuss: the NY Civil Fraud case including Trump being fined and being found to be a liar by the Judge as Michael Cohen reinforces the prior testimony of other witnesses, and the Court observing that Trump has already lied; a trial that starts on Monday to bar Trump from the Colorado ballot; updates in the DC Election interference case including the gag order being reimposed, motions to dismiss filed by Trump; updates in the Georgia prosecution of Trump, including another lawyer for Trump turning state’s evidence, and much more from the intersection of law politics and justice.&#13; &#13; DEALS FROM OUR SPONSOR!&#13; AG1: Go to https://drinkAG1.com/LEGALAF and get 5 free AG1 Travel Packs and a FREE 1 year supply of Vitamin D with your first purchase!&#13; Henry Meds: Get exclusive offers at https://HenryMeds.com/legalaf&#13; Rhone: Head to https://rhone.com/legalaf and use code LEGALAF to save 20% off your entire order!&#13; &#13; SUPPORT THE SHOW:&#13; Shop NEW LEGAL AF Merch at: https://store.meidastouch.com&#13; Join us on Patreon: https://patreon.com/meidastouch&#13; &#13; Remember to subscribe to ALL the MeidasTouch Network Podcasts:&#13; MeidasTouch: https://pod.link/1510240831&#13; Legal AF: https://pod.link/1580828595&#13; The PoliticsGirl Podcast: https://pod.link/1595408601&#13; The Influence Continuum: https://pod.link/1603773245&#13; Kremlin File: https://pod.link/1575837599&#13; Mea Culpa with Michael Cohen: https://pod.link/1530639447&#13; The Weekend Show: https://pod.link/1612691018&#13; American Psyop: https://pod.link/1652143101&#13; Burn the Boats: https://pod.link/1485464343&#13; Majority 54: https://pod.link/1309354521&#13; Political Beatdown: https://pod.link/1669634407&#13; Lights On with Jessica Denson: https://pod.link/1676844320&#13; Uncovered: https://pod.link/1690214260</media:description> <media:community> <media:starRating count=""498"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""0""/> </media:community> </media:group> </entry><entry> <id>yt:video:teiYiZ19xmU</id> <yt:videoId>teiYiZ19xmU</yt:videoId> <yt:channelId>UC9r9HYFxEQOBXSopFS61ZWg</yt:channelId> <title>Trump Co-Defendant GIVES UP HIS LIFE to Trump, it’s NOT LOOKING PRETTY</title> <link rel=""alternate"" href=""https://www.youtube.com/watch?v=teiYiZ19xmU""/> <author> <name>MeidasTouch</name> <uri>https://www.youtube.com/channel/UC9r9HYFxEQOBXSopFS61ZWg</uri> </author> <published>2023-10-28T18:30:47+00:00</published> <updated>2023-10-28T18:30:47+00:00</updated> <media:group> <media:title>Trump Co-Defendant GIVES UP HIS LIFE to Trump, it’s NOT LOOKING PRETTY</media:title> <media:content url=""https://www.youtube.com/v/teiYiZ19xmU?version=3"" type=""application/x-shockwave-flash"" width=""640"" height=""390""/> <media:thumbnail url=""https://i1.ytimg.com/vi/teiYiZ19xmU/hqdefault.jpg"" width=""480"" height=""360""/> <media:description>The Department of Justice pressure campaign against current Trump, Valet, and Butler and indicted, co-conspirator in Mar-a-Lago, Walt Nauta to get him to flip on Trump, continues. Michael Popok of Legal AF reports on the Trump lawyers, trying to intimidate Nauta to stay with Trump, while the DOJ continues to undermine the credibility of Nauta’s lawyer in front of a future jury, and signal to Nauta that he should come in from the cold, and turn witness for the prosecution. Get up to 40% off for a limited time when you go to https://shopbeam.com/LEGALAF to try Beam's best-selling Dream Powder! Visit https://meidastouch.com for more! Support the MeidasTouch Network: https://patreon.com/meidastouch Add the MeidasTouch Podcast: https://podcasts.apple.com/us/podcast/the-meidastouch-podcast/id1510240831 Buy MeidasTouch Merch: https://store.meidastouch.com Follow MeidasTouch on Twitter: https://twitter.com/meidastouch Follow MeidasTouch on Facebook: https://facebook.com/meidastouch Follow MeidasTouch on Instagram: https://instagram.com/meidastouch Follow MeidasTouch on TikTok: https://tiktok.com/@meidastouch</media:description> <media:community> <media:starRating count=""20797"" average=""5.00"" min=""1"" max=""5""/> <media:statistics views=""333938""/> </media:community> </media:group> </entry></feed>";

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
        result.Entries.Should().HaveCount(1);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntries()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessageMultipleEntries);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Should().HaveCount(5);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Id.Should().Be("yt:video:SdcFlRBp-Vc");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryVideoId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().VideoId.Should().Be("SdcFlRBp-Vc");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryChannelId()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().ChannelId.Should().Be("UCsT0YIqwnpJCM-mx7-gSA4Q");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryTitle()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Title.Should()
            .Be("Your dream job has a game plan | Karen Crisostomo | TEDxEDHECBusinessSchool");
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryLinkAlternative()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().LinkAlternative.Should()
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
        result.Entries.Single().AuthorName.Should().Be("TEDx Talks");
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryAuthorUri()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().AuthorUri.Should()
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
        result.Entries.Single().Published.Should().Be(DateTime.Parse("2023-10-28T14:00:17+00:00"));
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryUpdated()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Updated.Should().Be(DateTime.Parse("2023-10-28T18:45:32+00:00"));
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroup()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Should().NotBeNull();
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupTitle()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Title.Should()
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
        result.Entries.Single().Group.ContentUrl.Should()
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
        result.Entries.Single().Group.ContentWidth.Should().Be(640);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupContentHeight()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.ContentHeight.Should().Be(390);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupThumbnailUrl()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.ThumbnailUrl.Should()
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
        result.Entries.Single().Group.ThumbnailWidth.Should().Be(480);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupThumbnailHeight()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.ThumbnailHeight.Should().Be(360);
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupDescription()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Description.Should().StartWith("Through her engaging storytelling,");
    }


    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunity()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Community.Should().NotBeNull();
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingCount()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Community.StarRatingCount.Should().Be(598);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingAverage()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Community.StarRatingAverage.Should().Be(5.00m);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingMin()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Community.StarRatingMin.Should().Be(1);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStarRatingMax()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Community.StarRatingMax.Should().Be(5);
    }

    [Fact]
    public void Adapt_WithKnownMessage_MapsEntryGroupCommunityStatisticsViews()
    {
        // arrange
        var xml = XDocument.Parse(KnownNotificationMessage);
        // act
        var result = Sut.Adapt(xml);
        // assert
        result.Entries.Single().Group.Community.StatisticsViews.Should().Be(11808);
    }
}