using System;
using System.Collections.Generic;
using System.IO;

namespace Zhengyan.VectorDB;
public class BitmapFileManager
{
    // 分块大小（字节数）
    private const int ChunkSize = 1024; // 每次读取1024字节（8KB内存占用）

    // 从文件获取前N个指定值的比特索引（分块处理）
    public static int[] GetFirstNBitsFromFile(string filePath, int n, bool value, long offset = 0)
    {
        if (n <= 0)
            return Array.Empty<int>();
        
        int[] result = new int[n];
        for (int i = 0; i < n; i++)
            result[i] = -1;
        
        int foundCount = 0;
        long totalBits = 0;
        long skipBits = offset * 8; // 计算跳过的比特数
        
        // 文件存在时，分块读取并处理
        if (File.Exists(filePath))
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                long fileLength = fs.Length;
                totalBits = fileLength * 8L;
                
                // 如果偏移量超出文件长度，跳过读取
                if (offset < fileLength)
                {
                    // 定位到偏移位置
                    fs.Seek(offset, SeekOrigin.Begin);
                    
                    byte[] chunk = new byte[ChunkSize];
                    long bytesProcessed = 0;
                    long bytesToProcess = fileLength - offset;
                    
                    while (bytesProcessed < bytesToProcess && foundCount < n)
                    {
                        // 计算本次读取的字节数
                        int bytesToRead = (int)Math.Min(ChunkSize, bytesToProcess - bytesProcessed);
                        int bytesRead = fs.Read(chunk, 0, bytesToRead);
                        
                        // 处理当前块
                        ProcessChunk(chunk, bytesRead, ref result, ref foundCount, n, value, skipBits + bytesProcessed * 8);
                        
                        bytesProcessed += bytesRead;
                    }
                }
            }
        }
        
        // 如果找到的数量不足，追加字节并更新结果
        if (foundCount < n)
        {
            int needCount = n - foundCount;
            int appendBytes = (needCount + 7) / 8;
            byte fillByte = value ? (byte)0xFF : (byte)0x00;
            
            // 追加到文件
            using (FileStream fs = new FileStream(filePath, File.Exists(filePath) ? FileMode.Append : FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < appendBytes; i++)
                {
                    fs.WriteByte(fillByte);
                }
            }
            
            // 计算新追加比特的起始索引
            long startIndex = Math.Max(totalBits, skipBits);
            
            // 填充剩余结果
            for (int i = 0; i < needCount; i++)
            {
                result[foundCount + i] = (int)(startIndex + i);
            }
        }
        
        return result;
    }

    // 处理单个数据块
    private static void ProcessChunk(byte[] chunk, int bytesInChunk, ref int[] result, ref int foundCount, 
                                    int totalNeeded, bool targetValue, long startBitIndex)
    {
        for (int byteIndex = 0; byteIndex < bytesInChunk && foundCount < totalNeeded; byteIndex++)
        {
            byte currentByte = chunk[byteIndex];
            
            // 优化：跳过不可能匹配的字节
            if (targetValue && currentByte == 0x00) continue; // 找1但全0
            if (!targetValue && currentByte == 0xFF) continue; // 找0但全1
            
            for (int bitIndex = 7; bitIndex >= 0; bitIndex--)
            {
                if (foundCount >= totalNeeded) return;
                
                long globalIndex = startBitIndex + byteIndex * 8L + (7 - bitIndex);
                
                // 检查比特值
                bool bitValue = (currentByte & (1 << bitIndex)) != 0;
                
                if (bitValue == targetValue)
                {
                    result[foundCount] = (int)globalIndex;
                    foundCount++;
                }
            }
        }
    }

    // 分块设置比特位
    public static void SetBitsInFile(string filePath, int[] indices, bool value)
    {
        if (indices == null || indices.Length == 0)
            return;
        
        // 按字节索引分组
        var byteGroups = GroupIndicesByByte(indices);
        
        using (FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
        {
            foreach (var group in byteGroups)
            {
                int byteIndex = group.Key;
                byte mask = group.Value;
                
                // 定位到目标字节位置
                if (byteIndex >= fs.Length)
                {
                    // 如果位置超出当前文件长度，需要扩展文件
                    fs.SetLength(byteIndex + 1);
                }
                
                fs.Position = byteIndex;
                int currentByte = fs.ReadByte();
                if (currentByte == -1) currentByte = 0;
                
                // 应用位操作
                byte newValue;
                if (value)
                {
                    newValue = (byte)(currentByte | mask);
                }
                else
                {
                    newValue = (byte)(currentByte & ~mask);
                }
                
                // 写回修改
                fs.Position = byteIndex;
                fs.WriteByte(newValue);
            }
        }
    }

    // 按字节索引分组索引
    private static Dictionary<int, byte> GroupIndicesByByte(int[] indices)
    {
        var groups = new Dictionary<int, byte>();
        
        foreach (int index in indices)
        {
            int byteIndex = index / 8;
            int bitOffset = index % 8;
            int bitPosition = 7 - bitOffset;
            byte bitMask = (byte)(1 << bitPosition);
            
            if (groups.TryGetValue(byteIndex, out byte mask))
            {
                groups[byteIndex] = (byte)(mask | bitMask);
            }
            else
            {
                groups[byteIndex] = bitMask;
            }
        }
        
        return groups;
    }

    // 分块获取比特位
    public static bool[] GetBitsFromFile(string filePath, int[] indices)
    {
        if (indices == null)
            throw new ArgumentNullException(nameof(indices));
        
        bool[] results = new bool[indices.Length];
        
        // 按字节索引分组
        var byteGroups = GroupIndicesWithOriginalIndex(indices);
        
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            long fileLength = fs.Length;
            
            foreach (var group in byteGroups)
            {
                int byteIndex = group.Key;
                
                // 跳过超出文件范围的字节
                if (byteIndex >= fileLength)
                {
                    foreach (var item in group.Value)
                    {
                        results[item.origIndex] = false;
                    }
                    continue;
                }
                
                // 读取目标字节
                fs.Position = byteIndex;
                int currentByte = fs.ReadByte();
                if (currentByte == -1) currentByte = 0;
                
                // 处理该字节的所有比特位
                foreach (var item in group.Value)
                {
                    int bitPosition = 7 - item.bitOffset;
                    results[item.origIndex] = ((byte)currentByte & (1 << bitPosition)) != 0;
                }
            }
        }
        
        return results;
    }

    // 按字节索引分组索引（保留原始索引）
    private static Dictionary<int, List<(int origIndex, int bitOffset)>> GroupIndicesWithOriginalIndex(int[] indices)
    {
        var groups = new Dictionary<int, List<(int, int)>>();
        
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];
            int byteIndex = index / 8;
            int bitOffset = index % 8;
            
            if (!groups.TryGetValue(byteIndex, out var list))
            {
                list = new List<(int, int)>();
                groups[byteIndex] = list;
            }
            
            list.Add((i, bitOffset));
        }
        
        return groups;
    }
}