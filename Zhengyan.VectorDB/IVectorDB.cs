using ProtoBuf;

namespace Zhengyan.VectorDB;

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

public interface IVectorDB
{

    public class SearchResult
    {
        internal SearchResult(int id, float[] index, float distance, byte[] content)
        {
            Id = id;
            Index = index;
            Distance = distance;
            Content = content;
        }

        public int Id { get; }

        public float[] Index { get; }

        public float Distance { get; }

        public byte[] Content { get; }

        public override string ToString()
        {
            return $"{{\n\tId: {Id}, \n\tIndex: {Index.FormatString()}, \n\tDistance: {Distance}, \n\tContent: [{string.Join(',', Content)}]\n}}";
        }
    }

    IVectorDB AddItems(IEnumerable<(float[], byte[])> items);
    IList<SearchResult> SearchTopK(float[] query, int k);

    bool DeleteItems(int id);
    IVectorDB Save();
    void Delete();
}