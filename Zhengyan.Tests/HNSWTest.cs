using System.Numerics;
using HNSWIndex;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Zhengyan.Lunar;
using Zhengyan.Lunar.EightChar;
using Zhengyan.Lunar.Util;
using Zhengyan.WebSearch;
internal static class Utils
{
    internal static float Magnitude(this float[] vector)
    {
        float magnitude = 0.0f;
        int step = Vector<float>.Count;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }
        return (float)Math.Sqrt(magnitude);
    }

    internal static void Normalize(this float[] vector)
    {
        float normFactor = 1f / Magnitude(vector);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] *= normFactor;
        }
    }

    internal static List<float[]> RandomVectors(int vectorSize, int vectorsCount)
    {
        var random = new Random(vectorsCount);
        var vectors = new List<float[]>();

        for (int i = 0; i < vectorsCount; i++)
        {
            var vector = new float[vectorSize];
            for (int d = 0; d < vectorSize; d++)
                vector[d] = random.NextSingle();
            vectors.Add(vector);
        }

        return vectors;
    }

    internal static List<float[]> TestVectors(int vectorSize, int vectorsCount)
    {
        
        var vectors = new List<float[]>();

        for (int i = 0; i < vectorsCount; i++)
        {
            var vector = new float[vectorSize];
            for (int d = 0; d < vectorSize; d++)
                vector[d] = i + d * 0.1f; // Simple pattern for testing
            vectors.Add(vector);
        }

        return vectors;
    }
}
public class HNSWTest
{
    private static List<float[]>? vectors;

    public static void TestInitialize()
    {
        // vectors = Utils.RandomVectors(16, 10);
        vectors = Utils.TestVectors(16, 15);
    }

    public static void EncodeDecodeTest()
    {

        var index = new HNSWIndex<float[], float>(HNSWIndex.Metrics.SquaredEuclideanMetric.Compute);

        for (int i = 0; i < vectors.Count; i++)
            index.Add(vectors[i]);

        index.Serialize("GraphData.bin");

        // var index = HNSWIndex<float[], float>.Deserialize(HNSWIndex.Metrics.SquaredEuclideanMetric.Compute, "GraphData.bin");

        var decodedIndex = HNSWIndex<float[], float>.Deserialize(HNSWIndex.Metrics.SquaredEuclideanMetric.Compute, "GraphData.bin");
        
        decodedIndex.Remove(decodedIndex.KnnQuery(vectors[5], 1)[0].Id);

        for (int i = 0; i < vectors.Count; i++)
        {
            var originalResults = index.KnnQuery(vectors[i], 1);
            var decodeResults = decodedIndex.KnnQuery(vectors[i], 1);
            for (int j = 0; j < originalResults.Count; j++)
            {
                Console.WriteLine($"{originalResults[j].Id}, {decodeResults[j].Id}");
                Console.WriteLine($"[{string.Join(',', originalResults[j].Label)}],[{string.Join(',', decodeResults[j].Label)}]");
                Console.WriteLine($"[{originalResults[j].Distance}],[{decodeResults[j].Distance}]");
            }
        }
    }
    public static async Task Run(string[] args)
    {
        TestInitialize();
        EncodeDecodeTest();
    }
}