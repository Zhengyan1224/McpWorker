using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace Zhengyan.KnowledgeBase;

public static class Extensions
{

    public static bool IsEnglish(this string text)
    {
        var textspan = text.AsSpan();
        foreach (var t in textspan)
            if (t > 255 || t < 0)
                return false;
        return true;
    }

    public static bool IsEnglish(this IEnumerable<string> texts)
    {
        foreach (var t in texts)
            if (!t.IsEnglish())
                return false;
        return true;
    }

    public static bool IsEnglish(this char c)
    {
        return -1 < c && c < 256;
    }

    public static bool AddDependency(this ConcurrentDictionary<string, object> dependencyCollection, string name, object dependencyObj)
    {
        return dependencyCollection.TryAdd(name, dependencyObj);
    }

    public static D GetDependency<D>(this ConcurrentDictionary<string, object> dependencyCollection, string name)
    {
        object d = null;
        dependencyCollection.TryGetValue(name, out d);
        return (D)d;
    }

    public static bool RemoveDependency(this ConcurrentDictionary<string, object> dependencyCollection, string name)
    {
        return dependencyCollection.TryRemove(name, out var d);
    }


    public static byte[] ToBytes(this DataBase data)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            if (data is TextData)
            {
                writer.Write((byte)0x01);
                writer.Write(((TextData)data).Content);
            }
            else
                throw new ArgumentException($"Unsupported data type \'{data.GetType()}\'.");

            if (data.MetaData != null)
            {
                foreach (var md in data.MetaData)
                {
                    writer.Write(md.Key);
                    writer.Write(md.Value);
                }
            }

            return ms.ToArray();
        }

    }

    public static void CreateDirectory(this string dirpath)
    {
        if (!Directory.Exists(dirpath))
        {
            Directory.CreateDirectory(dirpath);
        }
    }
}
