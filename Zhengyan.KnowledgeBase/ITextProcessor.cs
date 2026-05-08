namespace Zhengyan.KnowledgeBase;

public interface ITextProcessor
{
    IEnumerable<string> Chunking(string text, int maxChunkSize);
    IEnumerable<string> SplitSentences(string text);
    string ExtractSummary(string text, int maxSentenceCount);
    string ExtractSummary(IEnumerable<string> sentences, int maxSentenceCount);
    IEnumerable<(string, float)> ExtractTags(string text, int topN);
}