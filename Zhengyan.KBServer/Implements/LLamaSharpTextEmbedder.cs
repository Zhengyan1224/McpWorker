using LLama;
using LLama.Common;
using Zhengyan.KnowledgeBase;

namespace Zhengyan.KBServer.Implements;

public class LLamaSharpTextEmbedder : ITextEmbedder
{
    public LLamaEmbedder LLamaEmbedder { get; private set; }
    // 使用 SemaphoreSlim 替代 object 作为锁
    private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);

    public LLamaSharpTextEmbedder(string modelFilePath)
    {
        var @params = new ModelParams(modelFilePath) { Embeddings = true };
        using var weights = LLamaWeights.LoadFromFile(@params);
        var embedder = new LLamaEmbedder(weights, @params);
        LLamaEmbedder = embedder;
    }

    public float[] Embedding(string text)
    {
        lock (_asyncLock)
        {
            return LLamaEmbedder.GetEmbeddings(text).Result.First();
        }
    }

    public async Task<float[]> EmbeddingAsync(string text)
    {
        // 异步等待获取锁，避免阻塞线程
        await _asyncLock.WaitAsync();
        try
        {
            return (await LLamaEmbedder.GetEmbeddings(text)).First();
        }
        finally
        {
            // 确保在完成后释放锁
            _asyncLock.Release();
        }
    }
}