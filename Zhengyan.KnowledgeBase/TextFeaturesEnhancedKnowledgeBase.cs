using System.Text;
using System.Text.RegularExpressions;
using Zhengyan.VectorDB;

namespace Zhengyan.KnowledgeBase;

public enum TextFeaturesMode
{
    None = 0,
    Summary = 1,
    Tags = 2,
}

public class TextFeaturesEnhancedKnowledgeBase : SimpleKnowledgeBase
{
    public ITextProcessor TextProcessor { get; set; }
    public TextFeaturesMode TextFeaturesMode { get; set; }

    public int EmbeddingTextLength { get; set; } = 50;

    public TextFeaturesEnhancedKnowledgeBase(ITextEmbedder textEmbedding, IVectorDB vectorDB,
     ITextProcessor textProcessor, TextFeaturesMode textFeaturesMode)
     : base(textEmbedding, vectorDB)
    {
        this.TextProcessor = textProcessor;
        this.TextFeaturesMode = textFeaturesMode;

        this.GetTextFeaturesHandler = GetTextFeaturesEnhancedHandler;
    }

    internal TextFeaturesEnhancedKnowledgeBase()
    {
        this.GetTextFeaturesHandler = GetTextFeaturesEnhancedHandler;
    }

    private async Task<float[]> GetTextFeaturesEnhancedHandler(string text, ITextEmbedder textEmbedder)
    {
        string featureText = null;

        if (TextFeaturesMode == TextFeaturesMode.Summary)
        {
            featureText = ExtractFeatureTextBySummary(text);
        }
        else if (TextFeaturesMode == TextFeaturesMode.Tags)
        {
            featureText = ExtractFeatureTextByTags(text);
        }
        else
        {
            featureText = text;
        }

        return await textEmbedder.EmbeddingAsync(featureText);
    }

    private string ExtractFeatureTextBySummary(string text)
    {
        string pattern = @"[\t\n\r]"; // 匹配制表符、换行符和回车符
        string summary = Regex.Replace(TextProcessor.ExtractSummary(text, EmbeddingTextLength), pattern, "");
        return summary[..Math.Min(summary.Length, EmbeddingTextLength)];
    }

    private string ExtractFeatureTextByTags(string text)
    {
        var tags = TextProcessor.ExtractTags(text, EmbeddingTextLength);
        var _tags = tags.Select((tag) => tag.Item1);
        string result = string.Join(',', _tags);
        return result[..Math.Min(result.Length, EmbeddingTextLength)];
    }
}
