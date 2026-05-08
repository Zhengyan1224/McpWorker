using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Zhengyan.FSServer.Models;
using NPOI.XWPF.UserModel;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
namespace Zhengyan.FSServer.Services;

delegate Task<string> ReadFileDelegate(string relativePath, CancellationToken cancellationToken);

public class FSService : IFSService
{
    private readonly StorageConfig _storageConfig;

    private readonly Dictionary<string, ReadFileDelegate> _readFileDelegates = new Dictionary<string, ReadFileDelegate>();

    public FSService(StorageConfig storageConfig)
    {
        _storageConfig = storageConfig;

        _readFileDelegates.Add(".txt", ReadTextFileAsync);
        _readFileDelegates.Add(".md", ReadTextFileAsync);
        _readFileDelegates.Add(".pdf", ReadPdfFileAsync);
        _readFileDelegates.Add(".docx", ReadDocxFileAsync);
        _readFileDelegates.Add(".xlsx", ReadXlsxFileAsync);
    }



    public async Task<string> ReadFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(relativePath).ToLower();
        if (_readFileDelegates.TryGetValue(ext, out var readDelegate))
        {
            return await readDelegate(relativePath, cancellationToken);
        }
        else
        {
            return $"Unsupported file type: {ext}";
        }

    }
    public async Task<string> UploadFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}{Path.GetExtension(file.FileName)}";
        var dateDir = DateTime.Now.ToString("yyyyMMdd");
        var returnPath = $"{dateDir}/{fileName}";
        var savePath = Path.Combine(_storageConfig.StorageBaseDir, returnPath);
        returnPath = returnPath.Replace("\\", "/");
        var subDir = Path.Combine(_storageConfig.StorageBaseDir, dateDir);
        if (!Directory.Exists(subDir))
        {
            Directory.CreateDirectory(subDir);
        }
        using (var stream = new FileStream(savePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        return returnPath;
    }

    #region Read File Implementations
    public async Task<string> ReadTextFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_storageConfig.StorageBaseDir, relativePath);
        if (!File.Exists(filePath))
        {
            return "File not found";
        }
        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (var reader = new StreamReader(stream))
        {
            return await reader.ReadToEndAsync();
        }
    }

    public async Task<string> ReadPdfFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_storageConfig.StorageBaseDir, relativePath);
        if (!File.Exists(filePath))
        {
            return "File not found";
        }

        // 使用 Task.Run 将同步的 PDF 读取操作放到线程池中执行
        return await Task.Run(() =>
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true)) // 使用 async 标志? 但这里同步读取
            using (var pdfDocument = PdfDocument.Open(stream))
            {
                var stringBuilder = new StringBuilder();
                foreach (var page in pdfDocument.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    stringBuilder.AppendLine(page.Text);
                }
                return stringBuilder.ToString();
            }
        }, cancellationToken);
    }

    public async Task<string> ReadDocxFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_storageConfig.StorageBaseDir, relativePath);
        if (!File.Exists(filePath))
        {
            return "File not found";
        }

        return await Task.Run(() =>
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                var document = new XWPFDocument(stream);
                var stringBuilder = new StringBuilder();

                // 遍历所有段落
                foreach (var para in document.Paragraphs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string text = para.ParagraphText;
                    if (!string.IsNullOrEmpty(text))
                    {
                        stringBuilder.AppendLine(text);
                    }
                }

                // 可选：提取表格中的文本（若需要可取消注释）
                foreach (var table in document.Tables)
                {
                    foreach (var row in table.Rows)
                    {
                        foreach (var cell in row.GetTableCells())
                        {
                            stringBuilder.Append(cell.GetText() + "\t");
                        }
                        stringBuilder.AppendLine();
                    }
                }

                return stringBuilder.ToString();
            }
        }, cancellationToken);
    }

    public async Task<string> ReadXlsxFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_storageConfig.StorageBaseDir, relativePath);
        if (!File.Exists(filePath))
        {
            return "File not found";
        }

        return await Task.Run(() =>
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                var workbook = new XSSFWorkbook(stream);
                var stringBuilder = new StringBuilder();
                var dataFormatter = new DataFormatter(); // 用于将单元格格式化为字符串

                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sheet = workbook.GetSheetAt(i);
                    stringBuilder.AppendLine($"===== Sheet: {sheet.SheetName} =====");

                    // 遍历所有行
                    for (int rowIdx = 0; rowIdx <= sheet.LastRowNum; rowIdx++)
                    {
                        var row = sheet.GetRow(rowIdx);
                        if (row == null) continue;

                        // 遍历所有单元格
                        for (int colIdx = 0; colIdx < row.LastCellNum; colIdx++)
                        {
                            var cell = row.GetCell(colIdx);
                            string cellValue = dataFormatter.FormatCellValue(cell); // 处理数值、日期等
                            stringBuilder.Append(cellValue);

                            // 列之间用制表符分隔
                            if (colIdx < row.LastCellNum - 1)
                                stringBuilder.Append('\t');
                        }
                        stringBuilder.AppendLine(); // 行结束换行
                    }
                    stringBuilder.AppendLine(); // 工作表之间空一行
                }

                return stringBuilder.ToString();
            }
        }, cancellationToken);
    }
    #endregion
}