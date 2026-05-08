
using System.Security.Policy;
using System.Text;
using JiebaNet.Analyser;
using JiebaNet.Segmenter;

namespace Zhengyan.KnowledgeBase;

public enum KeywordExtractorType
{
    None = 0,
    Tfidf = 1,
    TextRank = 2
}

public class DefaultTextProcessor : ITextProcessor
{
    private JiebaSegmenter _segmenter = new JiebaSegmenter();
    private KeywordExtractor _extractor;

    public KeywordExtractor KeywordExtractor
    {
        get => _extractor;
        set => _extractor = value;
    }
    public readonly HashSet<char> SentenceSplitChar = new HashSet<char>
    {
        '。', '！', '？', '.', '!', '?'
    };

    public DefaultTextProcessor(string jiebaConfigFileBaseDir = null, KeywordExtractorType extractorType = KeywordExtractorType.Tfidf)
    {
        if (!string.IsNullOrWhiteSpace(jiebaConfigFileBaseDir))
            ConfigManager.ConfigFileBaseDir = jiebaConfigFileBaseDir;

        if (extractorType == KeywordExtractorType.Tfidf)
            _extractor = new TfidfExtractor();
        else if (extractorType == KeywordExtractorType.TextRank)
            _extractor = new TextRankExtractor();
        else
            _extractor = null;
    }

    public IEnumerable<string> Chunking(string text, int maxChunkSize)
    {
        var sentences = SplitSentences(text);
        List<string> chunks = new List<string>();
        StringBuilder tempBuilder = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (tempBuilder.Length > 0 && (tempBuilder.Length + sentence.Length) > maxChunkSize)
            {
                chunks.Add(tempBuilder.ToString());
                tempBuilder.Clear();
                tempBuilder.Append(sentence);
            }
            else
            {
                tempBuilder.Append(sentence);
            }
        }

        if (tempBuilder.Length > 0)
            chunks.Add(tempBuilder.ToString());

        return chunks;
    }

    public string ExtractSummary(string text, int maxSentenceCount)
    {
        var sentences = SplitSentences(text);
        return ExtractSummary(sentences, maxSentenceCount);
    }

    public string ExtractSummary(IEnumerable<string> sentences, int maxSentenceCount)
    {
        // 词频统计
        var wordFrequencies = new Dictionary<string, int>();
        foreach (var sentence in sentences)
        {
            var words = _segmenter.Cut(sentence);
            foreach (var word in words)
            {
                if (wordFrequencies.ContainsKey(word))
                    wordFrequencies[word]++;
                else
                    wordFrequencies[word] = 1;
            }
        }

        // 计算每个句子的分数（基于词频）
        var sentenceScores = new Dictionary<string, int>();
        foreach (var sentence in sentences)
        {
            var words = _segmenter.Cut(sentence);
            int sentenceScore = words.Sum(word => wordFrequencies.ContainsKey(word) ? wordFrequencies[word] : 0);
            sentenceScores[sentence] = sentenceScore;
        }

        // 按分数排序并选取前N个句子
        var topSentences = sentenceScores.OrderByDescending(pair => pair.Value)
                                         .Take(maxSentenceCount)
                                         .Select(pair => pair.Key);

        // 返回摘要
        // return string.Join("。", topSentences) + "。";

        // char endChar = topSentences.IsEnglish() ? '.' : '。';
        // return string.Join(endChar, topSentences) + endChar;
        return string.Join("", topSentences);
    }

    public IEnumerable<string> SplitSentences(string text)
    {
        // return text.Split(SentenceSplitChar.ToArray(), StringSplitOptions.RemoveEmptyEntries);
        List<string> sentences = new List<string>();
        StringBuilder tempBuilder = new StringBuilder();
        var textspan = text.AsSpan();
        foreach (var t in textspan)
        {
            tempBuilder.Append(t);
            if (SentenceSplitChar.Contains(t))
            {
                sentences.Add(tempBuilder.ToString());
                tempBuilder.Clear();
            }
        }

        if (tempBuilder.Length > 0)
            sentences.Add(tempBuilder.ToString());

        return sentences;
    }

    public IEnumerable<(string, float)> ExtractTags(string text, int topN = 20)
    {
        if (_extractor == null)
            return null;
        IEnumerable<WordWeightPair> results = _extractor.ExtractTagsWithWeight(text, topN);
        List<(string, float)> tags = new List<(string, float)>();
        foreach (var p in results)
            tags.Add((p.Word, (float)p.Weight));

        return tags;
    }
}