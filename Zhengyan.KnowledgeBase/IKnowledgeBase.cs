using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace Zhengyan.KnowledgeBase;

public class DataBase
{
    public Dictionary<string, string>? MetaData { get; set; }

    public byte[] ToBytes()
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            if (this is TextData)
            {
                writer.Write((byte)0x01);
                writer.Write(((TextData)this).Content);
            }
            else
                throw new ArgumentException($"Unsupported data type \'{this.GetType()}\'.");

            if (this.MetaData != null)
            {
                foreach (var md in this.MetaData)
                {
                    writer.Write(md.Key);
                    writer.Write(md.Value);
                }
            }

            return ms.ToArray();
        }
    }

    public static DataBase FromBytes(byte[] bytes)
    {
        DataBase data = null;
        using (var ms = new MemoryStream(bytes))
        using (var reader = new BinaryReader(ms))
        {
            byte type = reader.ReadByte();
            if (type == 0x01)
            {
                data = new TextData() { Content = reader.ReadString(), MetaData = new Dictionary<string, string>() };
            }
            else
            {
                throw new ArgumentException($"Unsupported data type code \'{type}\'.");
            }

            while (ms.Position < ms.Length)
            {
                string k = reader.ReadString();
                string v = reader.ReadString();
                data.MetaData.TryAdd(k, v);
            }
        }
        return data;
    }
}

public class TextData : DataBase
{
    public string Content { get; set; }
}

public class SearchResult
{
    public int Id { get; set; }
    public float[] Index { get; set; }
    public float Distance { get; set; }
    public DataBase Data { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(
            value: this,
            options: new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true
            });
    }
}

public interface IKnowledgeBase
{
    Task<IKnowledgeBase> Add(TextData textData);
    Task<SearchResult[]> SearchTopKByText(string text, int k);
    bool DeleteItems(int id);
}

