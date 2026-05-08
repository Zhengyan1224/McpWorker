using System.Threading.Tasks;
using Zhengyan.VectorDB;

namespace Zhengyan.KnowledgeBase;


public delegate Task<float[]> GetTextFeatures(string text, ITextEmbedder textEmbedder);

public class SimpleKnowledgeBase : IKnowledgeBase
{
    public ITextEmbedder TextEmbedder { get; set; }
    public IVectorDB VectorDB { get; set; }

    public GetTextFeatures GetTextFeaturesHandler;

    public SimpleKnowledgeBase(ITextEmbedder textEmbedder, IVectorDB vectorDB)
    {
        this.TextEmbedder = textEmbedder;
        this.VectorDB = vectorDB;
    }

    internal SimpleKnowledgeBase() { }



    protected virtual async Task<float[]> DefaultGetTextFeaturesHandler(string text, ITextEmbedder textEmbedder)
    {
        return await textEmbedder.EmbeddingAsync(text);
    }

    public virtual async Task<IKnowledgeBase> Add(TextData textData)
    {
        var GetFeatures = GetTextFeaturesHandler == null ? DefaultGetTextFeaturesHandler : GetTextFeaturesHandler;
        float[] features = await GetFeatures(textData.Content, TextEmbedder);
        VectorDB.AddItems(new[] { (features, textData.ToBytes()) });
        return this;
    }

    public virtual async Task<SearchResult[]> SearchTopKByText(string text, int k)
    {
        var GetFeatures = GetTextFeaturesHandler == null ? DefaultGetTextFeaturesHandler : GetTextFeaturesHandler;
        float[] features = await GetFeatures(text, TextEmbedder);
        return SearchTopKByFeatures(features, k);
    }

    public virtual SearchResult[] SearchTopKByFeatures(float[] features, int k)
    {
        var rets = VectorDB.SearchTopK(features, k);
        SearchResult[] searchResults = new SearchResult[rets.Count];
        for (int i = 0; i < searchResults.Length; i++)
        {
            searchResults[i] = new SearchResult { Id = rets[i].Id, Index = rets[i].Index, Data = DataBase.FromBytes(rets[i].Content), Distance = rets[i].Distance };
        }
        return searchResults;
    }

    public bool DeleteItems(int id)
    {
        return VectorDB.DeleteItems(id);
    }
}
