namespace Zhengyan.KnowledgeBase;

public interface ITextEmbedder
{
    float[] Embedding(string text);
    Task<float[]> EmbeddingAsync(string text);
}