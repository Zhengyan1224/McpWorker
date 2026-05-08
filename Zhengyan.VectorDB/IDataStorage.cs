using System.Reflection.Metadata;

namespace Zhengyan.VectorDB;

public interface IDataStorage
{
    int[] AppendDatas(params byte[][] contents);
    byte[][] ReadDatas(params int[] positions);
    bool DeleteData(int position);
    bool DeleteAllData();
}