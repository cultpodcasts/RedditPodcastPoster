using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Converters;

namespace ContractExporter.Tests.Probes
{
    public enum OutsideKind
    {
        One
    }
}

namespace Api.Dtos
{
    public class SelfReferenceProbeDto
    {
        public SelfReferenceProbeDto? Child { get; set; }
    }

    public class OutsideBoundaryProbeDto
    {
        public ContractExporter.Tests.Probes.OutsideKind OutsideKind { get; set; }
    }

    public enum ItemEnumProbe
    {
        First,
        Second
    }

    public class ItemEnumProbeDto
    {
        [JsonConverter(typeof(ItemConverterDecorator<JsonStringEnumConverter>))]
        public List<ItemEnumProbe> Items { get; set; } = [];
    }

    public enum SplitEnumProbe
    {
        First,
        Second
    }

    public class ItemEnumSplitProbeDto
    {
        public SplitEnumProbe Bare { get; set; }

        [JsonConverter(typeof(ItemConverterDecorator<JsonStringEnumConverter>))]
        public List<SplitEnumProbe> Items { get; set; } = [];
    }
}
