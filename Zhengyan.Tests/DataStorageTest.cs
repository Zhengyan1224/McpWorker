using System.Numerics;
using System.Text;
using HNSWIndex;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Zhengyan.Lunar;
using Zhengyan.Lunar.EightChar;
using Zhengyan.Lunar.Util;
using Zhengyan.VectorDB;
using Zhengyan.WebSearch;

public class DataStorageTest
{
    private static string[] TestData = new string[]
    {
        "Hello, World!",
        "This is a test.",
        "Data storage is important.",
        "Zhengyan VectorDB is powerful.",
        "Testing data storage functionality.",
        "Ensure data integrity and performance.",
        "Data can be stored and retrieved efficiently.",
        "Zhengyan provides a robust solution.",
        "Testing with various data types.",
        "Data storage tests are essential.",
        "Performance testing is crucial.",
        "Zhengyan VectorDB supports large datasets.",
        "Data retrieval should be fast and reliable.",
        "Testing data consistency across operations.",
        "Zhengyan VectorDB can handle complex queries.",
        "Data storage tests help identify issues early.",
        "Zhengyan VectorDB is designed for scalability.",
    };
    private static LocalDiskDataStorage dataStorage = new LocalDiskDataStorage("data_storage");

    private static void WriteTestData()
    {
        foreach (string data in TestData)
        {
            // Store data
            int[] positions = dataStorage.AppendDatas(Encoding.UTF8.GetBytes(data));
            Console.WriteLine($"Stored data: {data} at position {positions.First()}");
        }
    }

    private static void DeleteTestData()
    {
        // Delete data at specific positions
        int[] deletePositions = new int[] { 2, 4 };
        foreach (int position in deletePositions)
        {
            bool success = dataStorage.DeleteData(position);
            Console.WriteLine($"Deleted data at position {position}: {success}");
        }
    }

    private static void AddBigData()
    {
        byte[] bigData = File.ReadAllBytes("./data_storage/test.md");
        int[] positions = dataStorage.AppendDatas(bigData);
        Console.WriteLine($"Stored big data at positions: {string.Join(", ", positions)}");
    }

    private static void ReadBigData()
    {
        var contents = dataStorage.ReadDatas(2);
        Console.WriteLine($"Read big data: {Encoding.UTF8.GetString(contents[0])}");
    }



    private static void ReadTestData()
    {
        int[] readPositions = new int[] { 13, 14, 15, 2, 4 };
        byte[][] readData = dataStorage.ReadDatas(readPositions);
        for (int i = 0; i < readData.Length; i++)
        {
            string data = Encoding.UTF8.GetString(readData[i]);
            Console.WriteLine($"Read data from position {readPositions[i]}: {data}");
        }
    }
    public static async Task Run(string[] args)
    {
        WriteTestData();
        ReadTestData();
        DeleteTestData();
        AddBigData();
        ReadBigData();
    }
}