using System.Runtime.InteropServices;

namespace Zhengyan.VectorDB;

public class LocalDiskDataStorage : IDataStorage
{
    [StructLayoutAttribute(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct DataBlock
    {
        /// <summary>
        /// 当值大于零时，表示该数据块的下一个数据块的位置。小于等于零时，其值的绝对值表示该数据内容的长度。
        /// </summary>
        public int NextBlockPosition;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = DataSize)]
        /// <summary>
        /// 数据内容
        /// </summary>
        public Byte[] Data;

        public static int Size => Marshal.SizeOf<DataBlock>();

        public const int DataSize = 1020; // 数据块的大小

        public override string ToString()
        {
            Data.FormatString();
            return $"{{\n\tNextBlockPosition: {NextBlockPosition}, \n\tData: [{string.Join(',', Data)}]\n}}";
        }
    }

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

    public LocalDiskDataStorage(string storageDirectoryPath)
    {
        this.StorageDirectoryPath = storageDirectoryPath;
    }

    public const string DataFileName = "data.bin";
    public const string DataMapFileName = "data.map";

    public string DataFilePath => Path.Combine(StorageDirectoryPath, DataFileName);
    public string DataMapFilePath => Path.Combine(StorageDirectoryPath, DataMapFileName);

    private ReaderWriterLockSlim dataRWLock = new ReaderWriterLockSlim();

    private long offset = 0;



    public int[] AppendDatas(params byte[][] contents)
    {
        int[] positions = new int[contents.Length];
        // throw new NotImplementedException();

        try
        {
            dataRWLock.EnterWriteLock();
            using (FileStream dataFileStream = new FileStream(DataFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {

                for (int i = 0; i < contents.Length; i++)
                {
                    var content = contents[i];
                    int n_block = content.Length / DataBlock.DataSize + (content.Length % DataBlock.DataSize == 0 ? 0 : 1);
                    if (n_block < 1)
                    {
                        positions[i] = -1; // 无效数据
                        continue;
                    }
                    // 获取前n个空闲位置
                    int[] _pos = BitmapFileManager.GetFirstNBitsFromFile(DataMapFilePath, n_block, false, offset);
                    positions[i] = _pos[0];
                    offset = _pos[^1] / 8L;

                    for (int j = 0; j < _pos.Length; j++)
                    {
                        DataBlock dataBlock = new DataBlock();
                        dataBlock.Data = content[(j * DataBlock.DataSize)..Math.Min((j + 1) * DataBlock.DataSize, content.Length)];

                        if (j < _pos.Length - 1)
                            dataBlock.NextBlockPosition = _pos[j + 1];
                        else
                        {
                            dataBlock.NextBlockPosition = -dataBlock.Data.Length;
                            if (dataBlock.Data.Length < DataBlock.DataSize)
                            {
                                dataBlock.Data = dataBlock.Data.Concat(new byte[DataBlock.DataSize - dataBlock.Data.Length]).ToArray();
                            }
                        }

                        WriteDataBlock(dataFileStream, _pos[j], dataBlock);
                        BitmapFileManager.SetBitsInFile(DataMapFilePath, _pos, true);
                    }
                }

                return positions;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error appending data: {e.Message}");
            return Array.Empty<int>();
        }
        finally
        {
            dataRWLock.ExitWriteLock();
        }

    }

    private void WriteDataBlock(Stream stream, int position, DataBlock dataBlock)
    {
        var bytes = dataBlock.StructToBytes();
        long offset = position * bytes.Length;
        if (stream.Length < offset + bytes.Length)
        {
            stream.SetLength(offset + bytes.Length);
        }
        stream.Seek(offset, SeekOrigin.Begin);
        stream.Write(bytes, 0, bytes.Length);
    }


    public bool DeleteData(int position)
    {
        try
        {
            dataRWLock.EnterWriteLock();
            List<long> filepositions = new List<long>();
            filepositions.Add(offset);
            int[] delete_positions;
            using (Stream stream = File.Open(DataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var dataBlocks = ReadContinuousDataBlocks(stream, position);
                delete_positions = (new int[] { position}).Concat(dataBlocks.Select(x => x.NextBlockPosition).Where(x => x > 0).ToArray()).ToArray();
            }

            filepositions.AddRange(delete_positions.Select(x => x / 8L));
            offset = filepositions.Min();

            // 删除数据块
            BitmapFileManager.SetBitsInFile(DataMapFilePath, delete_positions, false);

            

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error deleting data: {e.Message}");
            return false;
        }
        finally
        {
            dataRWLock.ExitWriteLock();
        }
    }

    public byte[][] ReadDatas(params int[] positions)
    {
        byte[][] datas = new byte[positions.Length][];
        try
        {
            using (Stream stream = File.Open(DataFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                for (int i = 0; i < positions.Length; i++)
                {
                    var dataBlocks = ReadContinuousDataBlocks(stream, positions[i]);
                    datas[i] = MergeDataBlocks(dataBlocks);
                }
                return datas;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error reading data: {e.Message}");
            return Array.Empty<byte[]>();
        }
    }

    private byte[] MergeDataBlocks(DataBlock[] blocks)
    {
        MemoryStream memoryStream = new MemoryStream();
        foreach (var block in blocks)
        {
            memoryStream.Write(block.Data, 0, block.NextBlockPosition < 0 ? -block.NextBlockPosition : block.Data.Length);
        }
        return memoryStream.ToArray();
    }

    

    private DataBlock[] ReadContinuousDataBlocks(Stream stream, int position)
    {
        DataBlock dataBlock = ReadDataBlock(stream, position);
        List<DataBlock> blocks = new List<DataBlock> { dataBlock };
        while (dataBlock.NextBlockPosition > 0)
        {
            dataBlock = ReadDataBlock(stream, dataBlock.NextBlockPosition);
            blocks.Add(dataBlock);
        }
        return blocks.ToArray();
    }

    private DataBlock ReadDataBlock(Stream stream, int position)
    {
        stream.Seek(position * DataBlock.Size, SeekOrigin.Begin);
        byte[] bytes = new byte[DataBlock.Size];
        int bytesRead = stream.Read(bytes, 0, bytes.Length);
        if (bytesRead < DataBlock.Size)
        {
            throw new EndOfStreamException("Reached end of stream before reading a complete DataBlock.");
        }
        return bytes.BytesToStuct<DataBlock>();
    }

    public bool DeleteAllData()
    {
        try
        {
            dataRWLock.EnterWriteLock();
            bool ret = false;
            if (File.Exists(DataFilePath))
            {
                File.Delete(DataFilePath);
                ret = true;
            }
            if (File.Exists(DataMapFilePath))
            {
                File.Delete(DataMapFilePath);
                ret = true;
            }
            return ret;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error deleting all data: {e.Message}");
            return false;
        }
        finally
        {
            dataRWLock.ExitWriteLock();
        }
    }

}