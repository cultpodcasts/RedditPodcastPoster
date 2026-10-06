using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Bluesky.Client;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class BlueskyCreateRecordMentionRules
{
    private const string DashedHandle = "@guest-name.example.test";
    private const string PlainHandle = "@guest.example.test";
    private const string Did = "did:plc:examplemention";
    private const string CreatedUri = "at://did:plc:example/app.bsky.feed.post/recordkey";

    [Fact(DisplayName =
        "A dashed Bluesky handle is restored on the createRecord text and receives a mention facet at UTF-8 byte offsets, because X.Bluesky truncates dashes and throws before the post is sent.")]
    public void dashed_handle_is_restored_with_a_mention_facet()
    {
        // Arrange
        const string text = "Hi 🎧 " + DashedHandle;
        var (masked, mentions) = CreateRecordMentionPatch.Mask(text);
        var body = CreateRecordBody(masked);
        var dids = new Dictionary<string, string> { [DashedHandle] = Did };

        // Act
        var restored = CreateRecordMentionPatch.Restore(body, mentions, dids);

        // Assert
        using var document = JsonDocument.Parse(restored);
        var record = document.RootElement.GetProperty("record");
        record.GetProperty("text").GetString().Should().Be(text);
        var mention = mentions.Should().ContainSingle().Subject;
        var byteStart = Encoding.UTF8.GetByteCount(masked.AsSpan(0, mention.CharIndex));
        byteStart.Should().BeGreaterThan(mention.CharIndex);
        var facet = record.GetProperty("facets").EnumerateArray().Should().ContainSingle().Subject;
        facet.GetProperty("index").GetProperty("byteStart").GetInt32().Should().Be(byteStart);
        facet.GetProperty("index").GetProperty("byteEnd").GetInt32().Should()
            .Be(byteStart + Encoding.UTF8.GetByteCount(DashedHandle));
        var feature = facet.GetProperty("features").EnumerateArray().Should().ContainSingle().Subject;
        feature.GetProperty("$type").GetString().Should().Be("app.bsky.richtext.facet#mention");
        feature.GetProperty("did").GetString().Should().Be(Did);
    }

    [Fact(DisplayName =
        "A dotted handle without a dash is masked and restored the same way, because a failed library mention lookup would throw and drop the post.")]
    public void dotted_handle_is_masked_before_the_library_sees_it()
    {
        // Arrange
        const string text = "Hello " + PlainHandle;
        var dids = new Dictionary<string, string> { [PlainHandle] = Did };

        // Act
        var (masked, mentions) = CreateRecordMentionPatch.Mask(text);
        var restored = CreateRecordMentionPatch.Restore(CreateRecordBody(masked), mentions, dids);

        // Assert
        masked.Should().NotContain("@");
        masked.Should().Contain("~guest.example.test");
        using var document = JsonDocument.Parse(restored);
        document.RootElement.GetProperty("record").GetProperty("text").GetString().Should().Be(text);
        document.RootElement.GetProperty("record").GetProperty("facets").GetArrayLength().Should().Be(1);
    }

    [Fact(DisplayName =
        "An unresolved handle is posted as plain text without a mention facet, because a missing DID must not fail the post.")]
    public void unresolved_handle_keeps_the_at_sign_and_omits_the_facet()
    {
        // Arrange
        const string text = "Hello " + DashedHandle;
        var (masked, mentions) = CreateRecordMentionPatch.Mask(text);

        // Act
        var restored = CreateRecordMentionPatch.Restore(
            CreateRecordBody(masked),
            mentions,
            new Dictionary<string, string>());

        // Assert
        using var document = JsonDocument.Parse(restored);
        var record = document.RootElement.GetProperty("record");
        record.GetProperty("text").GetString().Should().Be(text);
        record.GetProperty("facets").GetArrayLength().Should().Be(0);
    }

    [Fact(DisplayName =
        "A token without a domain dot is left unchanged, because it is not an atproto handle.")]
    public void bare_at_token_is_not_a_mention()
    {
        // Arrange
        const string text = "Hello @guest today";

        // Act
        var (masked, mentions) = CreateRecordMentionPatch.Mask(text);

        // Assert
        masked.Should().Be(text);
        mentions.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "The createRecord response AT URI is the uri field, because that value is stored for a later delete.")]
    public void created_uri_is_read_from_the_response()
    {
        // Arrange
        var response = "{\"uri\":\"" + CreatedUri + "\",\"cid\":\"bafy\"}";

        // Act
        var uri = CreateRecordMentionPatch.ReadCreatedUri(response);
        var missing = CreateRecordMentionPatch.ReadCreatedUri("{\"cid\":\"bafy\"}");

        // Assert
        uri.Should().Be(CreatedUri);
        missing.Should().BeNull();
    }

    [Fact(DisplayName =
        "Only the createRecord POST is rewritten and its response URI is captured, because handle lookup and card fetches must pass through unchanged.")]
    public async Task create_record_post_is_rewritten_and_other_requests_pass_through()
    {
        // Arrange
        const string text = "Hi " + DashedHandle;
        var (masked, mentions) = CreateRecordMentionPatch.Mask(text);
        var state = new CreateRecordEditState
        {
            Mentions = mentions
        };
        state.DidByHandle[DashedHandle] = Did;
        var capture = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"uri\":\"" + CreatedUri + "\"}", Encoding.UTF8, "application/json")
        });
        var handler = new CreateRecordCaptureHandler(state)
        {
            InnerHandler = capture
        };
        using var invoker = new HttpMessageInvoker(handler);

        // Act
        using var create = new HttpRequestMessage(
            HttpMethod.Post,
            "https://bsky.social/xrpc/com.atproto.repo.createRecord")
        {
            Content = new StringContent(CreateRecordBody(masked), Encoding.UTF8, "application/json")
        };
        using var created = await invoker.SendAsync(create, CancellationToken.None);
        var rewritten = capture.Bodies.Should().ContainSingle().Subject;
        capture.Bodies.Clear();
        using var lookup = new HttpRequestMessage(
            HttpMethod.Get,
            "https://bsky.social/xrpc/com.atproto.identity.resolveHandle?handle=guest-name.example.test");
        using var lookedUp = await invoker.SendAsync(lookup, CancellationToken.None);

        // Assert
        using var document = JsonDocument.Parse(rewritten);
        document.RootElement.GetProperty("record").GetProperty("text").GetString().Should().Be(text);
        state.CreatedUri.Should().Be(CreatedUri);
        capture.Bodies.Should().BeEmpty();
        lookedUp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string CreateRecordBody(string text)
    {
        return "{\"repo\":\"did:plc:example\",\"collection\":\"app.bsky.feed.post\",\"record\":{\"text\":"
               + JsonSerializer.Serialize(text)
               + ",\"facets\":[]}}";
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content != null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return responder(request);
        }
    }
}
