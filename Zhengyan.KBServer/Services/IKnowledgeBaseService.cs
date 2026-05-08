using Zhengyan.KBServer.Models;

public interface IKnowledgeBaseService
{
    Task<int> AddKnowledgeAsync(string dbName, KnowledgeContent[] knowledgeContents, int chunkSize = 0);
    Task<bool> DeleteKnowledgeAsync(string dbName, int id);
    Task<KnowledgeContent[]> SearchKnowledgeAsync(string dbName, string query, int topK);
    Task<bool> DeleteKnowledgeBaseAsync(string dbName);
    bool DeleteKnowledge(string dbName, int id);
}