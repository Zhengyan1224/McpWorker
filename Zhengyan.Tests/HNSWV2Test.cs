using System.Numerics;
using Zhengyan.HNSW;
using HNSWIndex;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ProtoBuf;
using Zhengyan.Lunar;
using Zhengyan.Lunar.EightChar;
using Zhengyan.Lunar.Util;
using Zhengyan.WebSearch;

[ProtoContract]
public class CVector
{
    [ProtoMember(1)]
    public bool IsUnit { get; set; } = false;

    private float[] vector;

    [ProtoMember(2)]
    public float[] Vector
    {
        get => vector;
        set
        {
            // if (value == null || value.Length < 1)
            //     throw new ArgumentException("Vector cannot be null or empty.");
            vector = value;
            if (value == null || value.Length < 1)
                return;
            if (IsUnit)
            {
                vector.Normalize();
            }
        }
    }

    [ProtoMember(3)]
    public bool Deleted { get; set; } = false;

    public CVector(float[] vector, bool isUnit = false)
    {
        IsUnit = isUnit;
        Vector = vector;
    }

    public CVector() { }

}

public class HNSWV2Test
{
    private static List<CVector>? vectors;

    internal static float UnitCompute(CVector a, CVector b) => CosineDistance.ForUnits(a.Vector, b.Vector);

    public static void TestInitialize()
    {
        // vectors = Utils.RandomVectors(16, 10);
        var _vs = Utils.TestVectors(16, 15);

        vectors = new List<CVector>();
        foreach (var v in _vs)
        {
            var cv = new CVector(v, true);
            vectors.Add(cv);
        }
    }

    public static void EncodeDecodeTest()
    {
        SmallWorld<CVector, float> index, decodedIndex;
        var @params = new SmallWorld<CVector, float>.Parameters();
        @params.M = 15;
        @params.LevelLambda = 1 / Math.Log(15);
        index = new SmallWorld<CVector, float>(UnitCompute, DefaultRandomGenerator.Instance, @params);

        index.AddItems(vectors);
        // index.Items[5].Deleted = true;
        using (var stream = File.Create("GraphData.bin"))
        {
            index.SerializeGraph(stream);

        }
        
        
        using (Stream stream = File.OpenRead("GraphData.bin"))
            // (decodedIndex, _) = SmallWorld<CVector, float>.DeserializeGraph(vectors, UnitCompute, DefaultRandomGenerator.Instance, stream);
            decodedIndex = SmallWorld<CVector, float>.DeserializeGraph(vectors, UnitCompute, DefaultRandomGenerator.Instance, stream);

        using (var stream = File.Create("GraphData.bin"))
        {
            decodedIndex.SerializeGraph(stream);

        }

        using (Stream stream = File.OpenRead("GraphData.bin"))
            // (decodedIndex, _) = SmallWorld<CVector, float>.DeserializeGraph(vectors, UnitCompute, DefaultRandomGenerator.Instance, stream);
            decodedIndex = SmallWorld<CVector, float>.DeserializeGraph(vectors, UnitCompute, DefaultRandomGenerator.Instance, stream);

        var results = decodedIndex.KNNSearch(vectors[5], 5,item => !item.Deleted);

        foreach(var result in results)
        {
            Console.WriteLine($"Id: {result.Id}, Distance: {result.Distance}");
        }
    }
    public static async Task Run(string[] args)
    {
        TestInitialize();
        EncodeDecodeTest();
    }
}
