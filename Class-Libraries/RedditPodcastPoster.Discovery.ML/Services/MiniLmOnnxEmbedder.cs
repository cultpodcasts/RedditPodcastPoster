using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace RedditPodcastPoster.Discovery.ML.Services;

public sealed class MiniLmOnnxEmbedder : IDisposable
{
    private readonly InferenceSession _session;
    private readonly BertTokenizer _tokenizer;
    private readonly int _maxLength;
    private readonly bool _usesTokenTypeIds;

    public MiniLmOnnxEmbedder(string onnxModelPath, string vocabPath, int maxLength = 256)
    {
        _maxLength = maxLength;
        _session = new InferenceSession(onnxModelPath);
        _tokenizer = BertTokenizer.Create(vocabPath);
        _usesTokenTypeIds = _session.InputMetadata.ContainsKey("token_type_ids");
    }

    public float[] Embed(string text)
    {
        var ids = _tokenizer.EncodeToIds(text);
        if (ids.Count > _maxLength)
        {
            ids = ids.Take(_maxLength).ToList();
        }

        var inputIds = new long[_maxLength];
        var attentionMask = new long[_maxLength];
        var tokenTypeIds = new long[_maxLength];

        for (var i = 0; i < ids.Count; i++)
        {
            inputIds[i] = ids[i];
            attentionMask[i] = 1;
        }

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, [1, _maxLength])),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, [1, _maxLength]))
        };

        if (_usesTokenTypeIds)
        {
            inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",
                new DenseTensor<long>(tokenTypeIds, [1, _maxLength])));
        }

        using var outputs = _session.Run(inputs);
        var outputTensor = outputs.First().AsTensor<float>();
        var dimensions = outputTensor.Dimensions.ToArray();

        var hiddenSize = DiscoveryFeatureBuilder.EmbeddingDimensions;
        var embedding = new float[hiddenSize];

        if (dimensions.Length == 2 && dimensions[1] == hiddenSize)
        {
            for (var dim = 0; dim < hiddenSize; dim++)
            {
                embedding[dim] = outputTensor[0, dim];
            }

            return embedding;
        }

        var sequenceLength = dimensions.Length >= 2 ? dimensions[1] : ids.Count;
        var tokenCount = 0f;
        for (var token = 0; token < sequenceLength; token++)
        {
            if (attentionMask[token] == 0)
            {
                continue;
            }

            tokenCount++;
            for (var dim = 0; dim < hiddenSize; dim++)
            {
                embedding[dim] += outputTensor[0, token, dim];
            }
        }

        if (tokenCount > 0)
        {
            for (var dim = 0; dim < hiddenSize; dim++)
            {
                embedding[dim] /= tokenCount;
            }
        }

        return embedding;
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
