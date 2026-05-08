using Zhengyan.KnowledgeBase;

namespace Zhengyan.KBServer.Implements;
public class CustomTextProcessor : DefaultTextProcessor
{
    public CustomTextProcessor(string jiebaConfigFileBaseDir = null, string extractorType = "Tfidf")
        : base(jiebaConfigFileBaseDir, Enum.TryParse(extractorType, true, out KeywordExtractorType extractor) ? extractor : KeywordExtractorType.Tfidf)
    {

    }
}