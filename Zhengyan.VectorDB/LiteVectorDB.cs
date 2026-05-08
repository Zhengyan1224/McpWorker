
using System.Text;
using HNSWIndex;

namespace Zhengyan.VectorDB;

public class LiteVectorDB : IVectorDB
{
    public const string GraphFileName = "graph.bin";
    public const string IndexFileName = "index.bin";
    // public const string DataFileName = "data.bin";

    private string storageDirectoryPath = string.Empty;

    public string StorageDirectoryPath
    {
        get => this.storageDirectoryPath;
        private set
        {
            value = Path.GetFullPath(value);
            if (!Directory.Exists(value))
            {
                Directory.CreateDirectory(value);
            }
            this.storageDirectoryPath = value;
        }
    }

    // public HNSWIndex<float[], float>? World { get; private set; }
    public HNSWIndex<CVector, float>? World { get; private set; }

    public Dictionary<int, int>? IndexTable { get; private set; }

    public string GraphFilePath => Path.Combine(StorageDirectoryPath, GraphFileName);
    public string IndexFilePath => Path.Combine(StorageDirectoryPath, IndexFileName);
    // public string DataFilePath => Path.Combine(StorageDirectoryPath, DataFileName);

    public int EmbeddingSize { get; private set; }

    public bool ForUnits { get; }

    // private ReaderWriterLockSlim dataRWLock = new ReaderWriterLockSlim();
    private ReaderWriterLockSlim indexRWLock = new ReaderWriterLockSlim();

    public IDataStorage DataStorage { get; set; }

    public readonly bool FreeVector;

    internal float UnitCompute(CVector a, CVector b)
    {
        if (a == null || b == null || a.Vector == null || b.Vector == null)
            return float.MaxValue;
        return HNSWIndex.Metrics.CosineMetric.UnitCompute(a.Vector, b.Vector);
    }
    internal float Compute(CVector a, CVector b)
    {
        if (a == null || b == null || a.Vector == null || b.Vector == null)
            return float.MaxValue;
        return HNSWIndex.Metrics.CosineMetric.Compute(a.Vector, b.Vector);
    }

    private LiteVectorDB(string storageDirectoryPath, IDataStorage dataStorage, int embeddingSize = 0, bool forUnits = false, bool freeVector = false)
    {
        this.ForUnits = forUnits;
        this.FreeVector = freeVector;
        this.DataStorage = dataStorage;

        var @params = new HNSWParameters<float>();


        if (this.ForUnits)
            World = new HNSWIndex<CVector, float>(UnitCompute, @params);
        else
            World = new HNSWIndex<CVector, float>(Compute, @params);
        IndexTable = new();
        this.EmbeddingSize = embeddingSize;

        this.StorageDirectoryPath = storageDirectoryPath;
    }

    public IVectorDB AddItems(IEnumerable<(float[], byte[])>? items)
    {
        if (items == null || items.Count() < 1)
            return this;
        var vectors = items.Select((item) =>
        {
            if (EmbeddingSize < 1)
                EmbeddingSize = item.Item1.Length;
            else if (EmbeddingSize != item.Item1.Length)
            {
                throw new Exception("Embedding size is not equal to vector size.");
            }

            // return item.Item1;
            return new CVector(item.Item1, ForUnits);

        }).ToList();

        var ilist = World?.Add(vectors) ?? new int[0];
        // var positions = AppendDatas(DataFilePath, items.Select(item => item.Item2).ToArray());
        var positions = DataStorage.AppendDatas(items.Select(item => item.Item2).ToArray());
        foreach (var it in ilist.Zip(positions))
        {
            IndexTable?.Add(it.First, it.Second);
        }

        return this;
    }

    public IList<IVectorDB.SearchResult> SearchTopK(float[] query, int k)
    {
        var results = World?.KnnQuery(new CVector(query, ForUnits), k, item => !item.Deleted);
        int[] positions = results.Select(r => IndexTable[r.Id]).ToArray();

        // var contents = ReadDatas(DataFilePath, positions);
        var contents = DataStorage.ReadDatas(positions);
        var searchResult = new List<IVectorDB.SearchResult>();
        foreach (var it in results?.Zip(contents) ?? new List<(KNNResult<CVector, float>, byte[])>())
        {
            searchResult.Add(new IVectorDB.SearchResult(it.First.Id, it.First.Label.Vector, it.First.Distance, it.Second));
        }

        return searchResult.OrderBy(r => r.Distance).ToList();
    }


    public IVectorDB Save()
    {
        try
        {
            indexRWLock.EnterWriteLock();
            // SaveNodeVectors();
            SaveIndex();
            SaveGraph();
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            indexRWLock.ExitWriteLock();
        }
        return this;
    }

    public void Delete()
    {
        try
        {
            indexRWLock.EnterWriteLock();
            // dataRWLock.EnterWriteLock();

            if (File.Exists(this.IndexFilePath))
                File.Delete(this.IndexFilePath);

            if (File.Exists(this.GraphFilePath))
                File.Delete(this.GraphFilePath);

            // if (File.Exists(this.DataFilePath))
            //     File.Delete(this.DataFilePath);
            DataStorage.DeleteAllData();
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            // dataRWLock.ExitWriteLock();
            indexRWLock.ExitWriteLock();
        }
    }

    private void SaveGraph(string graphFilePath, HNSWIndex<CVector, float>? world)
    {
        try
        {
            // indexRWLock.EnterWriteLock();
            world?.Serialize(graphFilePath);
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e.Message}\n{e.StackTrace}");
        }
        finally
        {
            // dataRWLock.ExitWriteLock();
            // indexRWLock.ExitWriteLock();
        }
    }

    private void SaveGraph() => SaveGraph(GraphFilePath, World);


    public static IVectorDB Load(string storageDirectoryPath, bool forUnits = false, bool freeVector = false)
    {
        LiteVectorDB vectorDB = new LiteVectorDB(storageDirectoryPath, new LocalDiskDataStorage(storageDirectoryPath), forUnits: forUnits, freeVector: freeVector);


        if (!File.Exists(vectorDB.IndexFilePath))
            throw new FileNotFoundException($"Could not find file '{vectorDB.IndexFilePath}'.");
        vectorDB.IndexTable = vectorDB.ReadIndexTable(vectorDB.IndexFilePath);

        if (!File.Exists(vectorDB.GraphFilePath))
            throw new FileNotFoundException($"Could not find file '{vectorDB.GraphFilePath}'.");
        vectorDB.World = vectorDB.ReadGraph(vectorDB.GraphFilePath);

        vectorDB.EmbeddingSize = vectorDB.World?.Items().FirstOrDefault()?.Vector.Length ?? 0;

        return vectorDB;
    }

    public static IVectorDB CreateNew(string storageDirectoryPath, bool forUnits = false, bool freeVector = false)
    {
        LiteVectorDB vectorDB = new LiteVectorDB(storageDirectoryPath, new LocalDiskDataStorage(storageDirectoryPath), forUnits: forUnits, freeVector: freeVector);

        vectorDB.Delete();

        return vectorDB;
    }

    private void NewIndexFile(string indexFilePath)
    {
        using (Stream stream = File.Open(indexFilePath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            WriteIndexFileHeader(writer);
        }
    }

    private void WriteIndexFileHeader(BinaryWriter writer)
    {
        writer.Write(Encoding.UTF8.GetBytes("INDEX"));
    }

    private void AppendIndices(string indexFilePath, IEnumerable<(int, int)>? indices)
    {
        if (indices == null)
            return;
        using (Stream stream = File.Open(indexFilePath, FileMode.Append, FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            if (stream.Position == 0)
                WriteIndexFileHeader(writer);
            foreach ((int, int) index in indices)
            {
                writer.Write(index.Item1);
                writer.Write(index.Item2);
            }
        }
    }

    private void SaveIndex()
    {
        NewIndexFile(IndexFilePath);
        AppendIndices(IndexFilePath, IndexTable?.Select(i => (i.Key, i.Value)));
    }


    private Dictionary<int, int> ReadIndexTable(string indexFilePath)
    {
        var headerFlag = "INDEX";
        using (Stream stream = File.Open(indexFilePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            byte[] header = reader.ReadBytes(headerFlag.Length);
            if (Encoding.UTF8.GetString(header) != headerFlag)
                throw new FileLoadException($"Index file does not start with '{headerFlag}'");


            Dictionary<int, int> indexTable = new Dictionary<int, int>();
            while (stream.Position < stream.Length)
            {
                int k = reader.ReadInt32();
                int v = reader.ReadInt32();
                indexTable.Add(k, v);
            }
            return indexTable;
        }
    }

    private HNSWIndex<CVector, float> ReadGraph(string graphFilePath)
    {

        if (this.ForUnits)
        {
            return HNSWIndex<CVector, float>.Deserialize(UnitCompute, graphFilePath);
        }
        return HNSWIndex<CVector, float>.Deserialize(Compute, graphFilePath);

    }

    public bool DeleteItems(int id)
    {
        var items = World?.Items();
        if (items == null)
            return false;
        if (id < 0 || id >= items.Count())
            return false;
        var item = items[id];
        if (item == null)
            return false;
        if (item.Deleted)
            return false; // Already deleted
        item.Deleted = true;
        if(this.FreeVector && id > 0)
            item.Vector = null; // Clear the vector to free memory，如果清除向量，会导致如果索引为0的向量被删除，重新加载的时候会报错。
        if (IndexTable == null || !IndexTable.ContainsKey(id))
            return false;
        int pos = IndexTable[id];
        DataStorage.DeleteData(pos);
        // World?.Remove(id);
        IndexTable.Remove(id);
        return true;
    }
}
