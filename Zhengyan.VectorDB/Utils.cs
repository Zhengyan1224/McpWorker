using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Zhengyan.VectorDB;

public static class Utils
{
    //// <summary>
    /// 结构体转byte数组
    /// </summary>
    /// <param name="structObj">要转换的结构体</param>
    /// <returns>转换后的byte数组</returns>
    public static byte[] StructToBytes<T>(this T structObj) where T : struct
    {
        //得到结构体的大小
        int size = Marshal.SizeOf(structObj);
        //创建byte数组
        byte[] bytes = new byte[Marshal.SizeOf<T>()];
        //分配结构体大小的内存空间
        IntPtr structPtr = Marshal.AllocHGlobal(size);
        //将结构体拷到分配好的内存空间
        Marshal.StructureToPtr(structObj, structPtr, false);
        //从内存空间拷到byte数组
        Marshal.Copy(structPtr, bytes, 0, size);
        //释放内存空间
        Marshal.FreeHGlobal(structPtr);
        //返回byte数组
        return bytes;
    }

    /// <summary>
    /// byte数组转结构体
    /// </summary>
    /// <param name="bytes">byte数组</param>
    /// <returns>转换后的结构体</returns>
    public static T BytesToStuct<T>(this byte[] bytes) where T : struct
    {
        //得到结构体的大小
        int size = Marshal.SizeOf<T>();
        //byte数组长度小于结构体的大小
        if (size > bytes.Length)
        {
            //返回空
            return default(T);
        }
        //分配结构体大小的内存空间
        IntPtr structPtr = Marshal.AllocHGlobal(size);
        //将byte数组拷到分配好的内存空间
        Marshal.Copy(bytes, 0, structPtr, size);
        //将内存空间转换为目标结构体
        T obj = Marshal.PtrToStructure<T>(structPtr);
        //释放内存空间
        Marshal.FreeHGlobal(structPtr);
        //返回结构体
        return obj;
    }

    public static string FormatString<T>(this T[] array)
    {
        return $"[{string.Join(',', array)}]";
    }

    public static int[] GetFirstNBitsWithValue(this byte[] bitmap, int n, bool value)
    {
        if (n <= 0)
            return Array.Empty<int>();

        int[] result = new int[n];
        // 初始化结果数组为-1
        for (int i = 0; i < n; i++)
            result[i] = -1;

        int foundCount = 0; // 已找到的匹配比特数量
        int totalBits = bitmap.Length * 8; // 总比特数

        for (int byteIndex = 0; byteIndex < bitmap.Length; byteIndex++)
        {
            byte currentByte = bitmap[byteIndex];

            // 优化：跳过不可能找到目标的字节
            if (value && currentByte == 0x00) // 找1但当前字节全0
                continue;
            if (!value && currentByte == 0xFF) // 找0但当前字节全1
                continue;

            // 从高位(bit7)到低位(bit0)检查每个比特位
            for (int bitIndex = 7; bitIndex >= 0; bitIndex--)
            {
                // 计算全局索引（高位优先）: byteIndex*8 + (7 - bitIndex)
                int globalIndex = byteIndex * 8 + (7 - bitIndex);

                // 检查当前比特位是否符合目标值
                bool bitValue = (currentByte & (1 << bitIndex)) != 0;

                if (bitValue == value)
                {
                    result[foundCount] = globalIndex;
                    foundCount++;

                    // 已找到足够的匹配比特
                    if (foundCount >= n)
                        return result;
                }
            }
        }

        return result;
    }

    public static void SetBits(this byte[] bitmap, int[] indices, bool value)
    {
        // 参数检查
        if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));
        if (indices == null) throw new ArgumentNullException(nameof(indices));

        // 创建字典按字节索引分组
        var byteGroups = new Dictionary<int, List<int>>();

        // 分组处理：相同字节索引的比特位归为一组
        foreach (int index in indices)
        {
            int byteIndex = index / 8;
            int bitOffset = index % 8;

            // 计算字节内的比特位置（从MSB到LSB）
            int bitPosition = 7 - bitOffset;

            if (!byteGroups.TryGetValue(byteIndex, out var positions))
            {
                positions = new List<int>();
                byteGroups[byteIndex] = positions;
            }

            positions.Add(bitPosition);
        }

        // 处理每个字节的比特设置
        foreach (var group in byteGroups)
        {
            int byteIndex = group.Key;
            var positions = group.Value;

            // 检查字节索引是否有效
            if (byteIndex < 0 || byteIndex >= bitmap.Length)
            {
                // 可以记录日志或抛出异常
                continue;
            }

            // 创建合并掩码
            byte mask = 0;
            foreach (int pos in positions)
            {
                mask |= (byte)(1 << pos);
            }

            // 应用位操作
            if (value)
            {
                // 设置比特为1：使用OR操作
                bitmap[byteIndex] |= mask;
            }
            else
            {
                // 设置比特为0：使用AND操作（带取反的掩码）
                bitmap[byteIndex] &= (byte)~mask;
            }
        }
    }

    public static bool[] GetBits(this byte[] bitmap, int[] indices)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));
        if (indices == null)
            throw new ArgumentNullException(nameof(indices));

        int totalBits = bitmap.Length * 8;
        bool[] results = new bool[indices.Length];

        // 使用字典按字节分组索引
        var byteGroups = new Dictionary<int, List<(int origIndex, int bitOffset)>>();

        // 第一步：收集并分组所有索引
        for (int i = 0; i < indices.Length; i++)
        {
            int index = indices[i];

            // 边界检查
            if (index < 0 || index >= totalBits)
                throw new IndexOutOfRangeException($"索引 {index} 超出位图范围 (0-{totalBits - 1})");

            int byteIndex = index / 8;
            int bitOffset = index % 8;

            if (!byteGroups.TryGetValue(byteIndex, out var group))
            {
                group = new List<(int, int)>();
                byteGroups[byteIndex] = group;
            }

            group.Add((i, bitOffset));
        }

        // 第二步：按字节处理比特值
        foreach (var kvp in byteGroups)
        {
            int byteIndex = kvp.Key;
            byte currentByte = bitmap[byteIndex];
            var group = kvp.Value;

            foreach (var item in group)
            {
                int origIndex = item.origIndex;
                int bitOffset = item.bitOffset;
                int bitPosition = 7 - bitOffset;

                // 提取比特值
                results[origIndex] = (currentByte & (1 << bitPosition)) != 0;
            }
        }

        return results;
    }

    /// <summary>
    /// 按照主机序列打印byte数组的二进制
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string ToBinaryString(this byte[] bytes)
    {
        // return string.Join("", bytes.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));
        StringBuilder sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.Append(Convert.ToString(b, 2).PadLeft(8, '0'));
            sb.Append(' ');
        }
        return sb.ToString();
    }

    public static float Magnitude(this float[] vector)
    {
        float magnitude = 0.0f;
        int step = Vector<float>.Count;
        for (int i = 0; i < vector.Length; i++)
        {
            magnitude += vector[i] * vector[i];
        }
        return (float)Math.Sqrt(magnitude);
    }

    public static void Normalize(this float[] vector)
    {
        float normFactor = 1f / vector.Magnitude();
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] *= normFactor;
        }
    }

}