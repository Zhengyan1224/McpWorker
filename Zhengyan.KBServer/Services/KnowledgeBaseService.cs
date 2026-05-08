using Zhengyan.KBServer.Models;
using Zhengyan.KnowledgeBase;

public class KnowledgeBaseService : IKnowledgeBaseService
{
    private readonly ILogger<KnowledgeBaseService> _logger;
    private readonly KnowledgeBaseManager<TextFeaturesEnhancedKnowledgeBase> knowledgeBaseManager;

    public KnowledgeBaseService(ILogger<KnowledgeBaseService> logger, KnowledgeBaseManager<TextFeaturesEnhancedKnowledgeBase> knowledgeBaseManager)
    {
        _logger = logger;
        this.knowledgeBaseManager = knowledgeBaseManager;
    }

    public async Task<int> AddKnowledgeAsync(string dbName, KnowledgeContent[] knowledgeContents, int chunkSize = 0)
    {
        int insertSuccessCount = 0;
        try
        {

            var kb = this.knowledgeBaseManager.GetKnowledgeBase(dbName);

            foreach (var kc in knowledgeContents)
            {

                var texts = chunkSize > 0 ? kb.TextProcessor.Chunking(kc.Content, chunkSize) : new string[] { kc.Content };

                foreach (var text in texts)
                {
                    try
                    {
                        await kb.Add(new TextData { Content = text, MetaData = kc.MetaData });
                        insertSuccessCount++;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, e.Message);
                    }
                }

            }

            this.knowledgeBaseManager.SaveKnowledgeBase(dbName);
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
        }
        return insertSuccessCount;
    }

    public async Task<bool> DeleteKnowledgeAsync(string dbName, int id)
    {
        return await Task<bool>.Run(() =>
        {
            var kb = this.knowledgeBaseManager.GetKnowledgeBase(dbName);
            bool ret = kb.DeleteItems(id);
            this.knowledgeBaseManager.SaveKnowledgeBase(dbName);
            return ret;
        });
    }

    public bool DeleteKnowledge(string dbName, int id)
    {
        var kb = this.knowledgeBaseManager.GetKnowledgeBase(dbName);
        bool ret = kb.DeleteItems(id);
        this.knowledgeBaseManager.SaveKnowledgeBase(dbName);
        return ret;
    }

    public async Task<bool> DeleteKnowledgeBaseAsync(string dbName)
    {
        return await Task<bool>.Run(() => this.knowledgeBaseManager.DeleteKnowledgeBase(dbName));
    }

    public async Task<KnowledgeContent[]> SearchKnowledgeAsync(string dbName, string query, int topK)
    {
        try
        {
            var kb = this.knowledgeBaseManager.GetKnowledgeBase(dbName);

            KnowledgeContent[] knowledgeContents = null;

            var searchResults = await kb.SearchTopKByText(query, topK);

            if (searchResults != null)
            {

                knowledgeContents = new KnowledgeContent[searchResults.Length];

                for (int i = 0; i < knowledgeContents.Length; i++)
                {
                    var sr = searchResults[i];
                    var kc = new KnowledgeContent() { Id = sr.Id, Distance = sr.Distance, MetaData = sr.Data.MetaData };

                    if (sr.Data is TextData)
                    {
                        kc.Content = ((TextData)sr.Data).Content;
                    }

                    knowledgeContents[i] = kc;
                }
            }

            return knowledgeContents;
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            return null;
        }
    }
}