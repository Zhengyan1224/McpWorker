using System.Net.Http.Headers;
using System.Runtime;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.VisualBasic.FileIO;
using Serilog;
using Zhengyan.McpServer.DataQuery.Config;
using Zhengyan.McpServer.DataQuery.Models;
using Zhengyan.VectorDB;

namespace Zhengyan.McpServer.DataQuery.Services;

public class DataQueryService : IDataQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly DataQueryConfig _config;
    private readonly string _dataDirectoryPath;
    private readonly string _cacheDirectoryPath;
    private readonly string _manifestFilePath;
    private readonly string _recordsCacheFilePath;
    private readonly string _vectorIndexDirectoryPath;
    private readonly HttpClient _embeddingHttpClient;

    private List<IndexedRecord> _records = [];
    private Dictionary<string, IndexedRecord> _recordLookup = new(StringComparer.OrdinalIgnoreCase);
    private bool _embeddingEnabled;
    private IVectorDB? _vectorDb;

    public static bool HasCompatibleCache(DataQueryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var dataDirectoryPath = ResolveDirectoryPath(config.DataDirectoryPath);
        var cacheDirectoryPath = ResolveDirectoryPath(config.CacheDirectoryPath);
        var manifestFilePath = Path.Combine(cacheDirectoryPath, "manifest.json");
        var recordsCacheFilePath = Path.Combine(cacheDirectoryPath, "records.json");

        if (!File.Exists(manifestFilePath) || !File.Exists(recordsCacheFilePath))
        {
            return false;
        }

        if (config.Embedding.Enabled)
        {
            var vectorIndexDirectoryPath = Path.Combine(cacheDirectoryPath, "vector_index");
            var requiredVectorFiles = new[]
            {
                Path.Combine(vectorIndexDirectoryPath, LiteVectorDBV2.GraphFileName),
                Path.Combine(vectorIndexDirectoryPath, LiteVectorDBV2.IndexFileName),
                Path.Combine(vectorIndexDirectoryPath, LiteVectorDBV2.VectorFileName),
                Path.Combine(vectorIndexDirectoryPath, LocalDiskDataStorage.DataFileName),
                Path.Combine(vectorIndexDirectoryPath, LocalDiskDataStorage.DataMapFileName)
            };

            if (requiredVectorFiles.Any(filePath => !File.Exists(filePath)))
            {
                return false;
            }
        }

        try
        {
            var currentManifest = BuildCurrentManifest(config, dataDirectoryPath);
            var cachedManifest = JsonSerializer.Deserialize<DataQueryCacheManifest>(File.ReadAllText(manifestFilePath), JsonOptions);
            return cachedManifest != null && ManifestEquals(cachedManifest, currentManifest);
        }
        catch
        {
            return false;
        }
    }

    public DataQueryService(DataQueryConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _dataDirectoryPath = ResolveDirectoryPath(config.DataDirectoryPath);
        _cacheDirectoryPath = ResolveDirectoryPath(config.CacheDirectoryPath);
        _manifestFilePath = Path.Combine(_cacheDirectoryPath, "manifest.json");
        _recordsCacheFilePath = Path.Combine(_cacheDirectoryPath, "records.json");
        _vectorIndexDirectoryPath = Path.Combine(_cacheDirectoryPath, "vector_index");
        _embeddingEnabled = config.Embedding.Enabled;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Directory.CreateDirectory(_cacheDirectoryPath);

        _embeddingHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, config.Embedding.TimeoutSeconds))
        };
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken);
        try
        {
            var currentManifest = BuildCurrentManifest();
            _embeddingEnabled = _config.Embedding.Enabled;
            DisposeVectorDb();

            if (TryLoadCache(currentManifest, out var cachedRecords))
            {
                _records = cachedRecords;
                _recordLookup = _records.ToDictionary(item => item.CacheKey, StringComparer.OrdinalIgnoreCase);
                Log.Information("DataQueryService loaded cache for {RecordCount} records from {CacheDirectoryPath}. EmbeddingEnabled = {EmbeddingEnabled}", _records.Count, _cacheDirectoryPath, _embeddingEnabled);
                return;
            }

            _records = await LoadRecordsAsync(cancellationToken);
            _recordLookup = _records.ToDictionary(item => item.CacheKey, StringComparer.OrdinalIgnoreCase);

            if (_embeddingEnabled && _records.Count > 0)
            {
                if (_records.Count > _config.MaxRecordsForEmbedding)
                {
                    Log.Warning("Record count {RecordCount} exceeds MaxRecordsForEmbedding {MaxRecordsForEmbedding}. Falling back to lexical retrieval.", _records.Count, _config.MaxRecordsForEmbedding);
                    _embeddingEnabled = false;
                }
                else
                {
                    try
                    {
                        await BuildVectorIndexAsync(_records, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _embeddingEnabled = false;
                        DisposeVectorDb();
                        Log.Warning(ex, "Failed to initialize vector index. Falling back to lexical retrieval.");
                    }
                }
            }

            currentManifest.RecordCount = _records.Count;
            SaveRecordCache(currentManifest, _records);
            Log.Information("DataQueryService loaded {RecordCount} records from {DataDirectoryPath}. EmbeddingEnabled = {EmbeddingEnabled}", _records.Count, _dataDirectoryPath, _embeddingEnabled);
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    public async Task<IReadOnlyList<DataSourceInfo>> ListDataSourcesAsync(CancellationToken cancellationToken = default)
    {
        if (_records.Count == 0)
        {
            await ReloadAsync(cancellationToken);
        }

        return _records
            .GroupBy(record => record.SourceFile, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DataSourceInfo
            {
                SourceFile = group.Key,
                RecordCount = group.Count()
            })
            .ToArray();
    }

    public async Task<DataQueryResponse> QueryAsync(
        string query,
        int? topN = null,
        IReadOnlyCollection<string>? sourceFiles = null,
        double? minSimilarity = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedMinSimilarity = NormalizeMinSimilarity(minSimilarity);
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateResponse(query, normalizedTopN: _config.DefaultTopN, normalizedMinSimilarity, filteredRecords: [], sourceFiles: sourceFiles, results: [], candidateCount: 0, searchMode: "none");
        }

        if (_records.Count == 0)
        {
            await ReloadAsync(cancellationToken);
        }

        var normalizedTopN = topN.GetValueOrDefault(_config.DefaultTopN);
        if (normalizedTopN <= 0)
        {
            normalizedTopN = _config.DefaultTopN;
        }
        normalizedTopN = Math.Min(normalizedTopN, Math.Max(_config.MaxTopN, 1));

        var filteredRecords = FilterRecords(sourceFiles);
        if (filteredRecords.Count == 0)
        {
            return CreateResponse(query, normalizedTopN, normalizedMinSimilarity, filteredRecords, sourceFiles, [], 0, _embeddingEnabled && _vectorDb != null ? "vector" : "lexical");
        }

        var normalizedQuery = NormalizeText(query);
        var queryTokens = Tokenize(normalizedQuery);

        if (_embeddingEnabled && _vectorDb != null)
        {
            try
            {
                var queryEmbedding = (await RequestEmbeddingsAsync([TrimForEmbedding(BuildEmbeddingText(query))], cancellationToken)).FirstOrDefault();
                if (queryEmbedding != null)
                {
                    return QueryWithVectorIndex(query, queryEmbedding, normalizedQuery, queryTokens, filteredRecords, sourceFiles, normalizedTopN, normalizedMinSimilarity);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to create query embedding. Falling back to lexical scoring for this request.");
            }
        }

        return QueryLexically(query, normalizedQuery, queryTokens, filteredRecords, sourceFiles, normalizedTopN, normalizedMinSimilarity);
    }

    private DataQueryResponse QueryWithVectorIndex(
        string query,
        float[] queryEmbedding,
        string normalizedQuery,
        HashSet<string> queryTokens,
        IReadOnlyList<IndexedRecord> filteredRecords,
        IReadOnlyCollection<string>? sourceFiles,
        int topN,
        double minSimilarity)
    {
        var filteredKeys = filteredRecords.Select(record => record.CacheKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateCount = filteredRecords.Count == _records.Count
            ? Math.Min(Math.Max(topN * Math.Max(_config.ApproximateSearchCandidateMultiplier, 1), topN), _records.Count)
            : filteredRecords.Count;
        var candidates = new List<(IndexedRecord Record, double Score)>();

        foreach (var result in _vectorDb!.SearchTopK(queryEmbedding, candidateCount))
        {
            var cacheKey = Encoding.UTF8.GetString(result.Content);
            if (!filteredKeys.Contains(cacheKey))
            {
                continue;
            }

            if (!_recordLookup.TryGetValue(cacheKey, out var record))
            {
                continue;
            }

            var lexicalScore = CalculateLexicalScore(normalizedQuery, queryTokens, record);
            var semanticScore = Math.Max(0d, 1d - result.Distance);
            var finalScore = semanticScore * 0.88 + lexicalScore * 0.12;
            candidates.Add((record, finalScore));
        }

        var results = candidates
            .Where(item => item.Score >= minSimilarity)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Record.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Record.RowNumber)
            .Take(topN)
            .Select(item => ToResult(item.Record, item.Score))
            .ToArray();

        return CreateResponse(query, topN, minSimilarity, filteredRecords, sourceFiles, results, candidates.Count, "vector");
    }

    private DataQueryResponse QueryLexically(
        string query,
        string normalizedQuery,
        HashSet<string> queryTokens,
        IReadOnlyList<IndexedRecord> filteredRecords,
        IReadOnlyCollection<string>? sourceFiles,
        int topN,
        double minSimilarity)
    {
        var candidates = filteredRecords
            .Select(record => (Record: record, Score: CalculateLexicalScore(normalizedQuery, queryTokens, record)))
            .Where(item => item.Score > 0)
            .ToArray();

        var results = candidates
            .Where(item => item.Score >= minSimilarity)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Record.SourceFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Record.RowNumber)
            .Take(topN)
            .Select(item => ToResult(item.Record, item.Score))
            .ToArray();

        return CreateResponse(query, topN, minSimilarity, filteredRecords, sourceFiles, results, candidates.Length, "lexical");
    }

    private IReadOnlyList<IndexedRecord> FilterRecords(IReadOnlyCollection<string>? sourceFiles)
    {
        if (sourceFiles == null || sourceFiles.Count == 0)
        {
            return _records;
        }

        var normalizedSet = sourceFiles
            .Where(sourceFile => !string.IsNullOrWhiteSpace(sourceFile))
            .Select(sourceFile => sourceFile.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedSet.Count == 0)
        {
            return _records;
        }

        return _records
            .Where(record => normalizedSet.Contains(record.SourceFile))
            .ToArray();
    }

    private async Task<List<IndexedRecord>> LoadRecordsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_dataDirectoryPath))
        {
            throw new DirectoryNotFoundException($"Data directory not found: {_dataDirectoryPath}");
        }

        var records = new List<IndexedRecord>();
        var extensions = new[] { ".csv", ".db" };
				foreach (var filePath in extensions
				    .SelectMany(ext => Directory.GetFiles(_dataDirectoryPath, $"*{ext}", System.IO.SearchOption.TopDirectoryOnly))
				    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
				{
				    cancellationToken.ThrowIfCancellationRequested();
				    records.AddRange(ReadCsvRecords(filePath));
				}

        return records;
    }

    private IEnumerable<IndexedRecord> ReadCsvRecords(string filePath)
    {
        var encoding = DetectEncoding(filePath);
        using var parser = new TextFieldParser(filePath, encoding)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (parser.EndOfData)
        {
            yield break;
        }

        var headers = NormalizeHeaders(parser.ReadFields());
        var rowNumber = 0;
        while (!parser.EndOfData)
        {
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException ex)
            {
                Log.Warning(ex, "Skip malformed csv line. File: {FilePath}, LineNumber: {LineNumber}", filePath, rowNumber + 2);
                continue;
            }

            if (fields == null)
            {
                continue;
            }

            rowNumber++;
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headers.Count; i++)
            {
                dict[headers[i]] = i < fields.Length ? fields[i]?.Trim() ?? string.Empty : string.Empty;
            }

            var sourceFile = Path.GetFileName(filePath);
            var searchText = BuildSearchText(sourceFile, dict);
            yield return CreateIndexedRecord(sourceFile, rowNumber, dict, searchText);
        }
    }

    private async Task BuildVectorIndexAsync(IReadOnlyList<IndexedRecord> records, CancellationToken cancellationToken)
    {
        string[]? texts = null;
        List<float[]>? vectors = null;
        List<(float[], byte[])>? items = null;
        IVectorDB? builtVectorDb = null;

        try
        {
            texts = records.Select(record => TrimForEmbedding(record.SearchText)).ToArray();
            vectors = await RequestEmbeddingsAsync(texts, cancellationToken);
            if (vectors.Count != records.Count)
            {
                throw new InvalidOperationException($"Embedding count mismatch. expected={records.Count}, actual={vectors.Count}");
            }

            items = new List<(float[], byte[])>(records.Count);
            for (var i = 0; i < records.Count; i++)
            {
                items.Add((vectors[i], Encoding.UTF8.GetBytes(records[i].CacheKey)));
            }

            builtVectorDb = LiteVectorDBV2.CreateNew(_vectorIndexDirectoryPath, forUnits: true, freeVector: false)
                .AddItems(items)
                .Save();
        }
        finally
        {
            builtVectorDb = null;
            texts = null;
            vectors = null;
            items = null;
        }

        // Reopen the persisted index so the runtime instance matches the low-memory cache-load path.
        CompactManagedMemory();
        _vectorDb = LoadVectorDbFromCache();
    }

    private async Task<List<float[]>> RequestEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return [];
        }

        var batchSize = Math.Max(1, _config.EmbeddingBatchSize);
        var result = new List<float[]>(inputs.Count);
        for (var offset = 0; offset < inputs.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = inputs.Skip(offset).Take(batchSize).ToArray();
            using var request = new HttpRequestMessage(HttpMethod.Post, _config.Embedding.Endpoint);
            if (!string.IsNullOrWhiteSpace(_config.Embedding.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.Embedding.ApiKey);
            }

            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = _config.Embedding.Model,
                input = batch
            }, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await _embeddingHttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var dataArray = document.RootElement.GetProperty("data").EnumerateArray()
                .OrderBy(item => item.TryGetProperty("index", out var indexElement) ? indexElement.GetInt32() : 0);

            foreach (var item in dataArray)
            {
                var vector = item.GetProperty("embedding").EnumerateArray().Select(number => number.GetSingle()).ToArray();
                result.Add(vector);
            }
        }

        return result;
    }

    private bool TryLoadCache(DataQueryCacheManifest currentManifest, out List<IndexedRecord> records)
    {
        records = [];

        if (!File.Exists(_manifestFilePath) || !File.Exists(_recordsCacheFilePath))
        {
            return false;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<DataQueryCacheManifest>(File.ReadAllText(_manifestFilePath), JsonOptions);
            if (manifest == null || !ManifestEquals(manifest, currentManifest))
            {
                return false;
            }

            var cachedRecords = JsonSerializer.Deserialize<List<CachedDataRecord>>(File.ReadAllText(_recordsCacheFilePath), JsonOptions);
            if (cachedRecords == null)
            {
                return false;
            }

            records = cachedRecords
                .Select(item => CreateIndexedRecord(item.SourceFile, item.RowNumber, item.Fields, item.SearchText, item.CacheKey))
                .ToList();

            if (_embeddingEnabled)
            {
                try
                {
                    _vectorDb = LoadVectorDbFromCache();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Cached vector index could not be loaded. It will be rebuilt.");
                    DisposeVectorDb();
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load data query cache. It will be rebuilt.");
            DisposeVectorDb();
            return false;
        }
    }

    private void SaveRecordCache(DataQueryCacheManifest manifest, IReadOnlyList<IndexedRecord> records)
    {
        Directory.CreateDirectory(_cacheDirectoryPath);
        var cacheRecords = records.Select(record => new CachedDataRecord
        {
            CacheKey = record.CacheKey,
            SourceFile = record.SourceFile,
            RowNumber = record.RowNumber,
            Fields = new Dictionary<string, string>(record.Fields, StringComparer.OrdinalIgnoreCase),
            SearchText = record.SearchText
        }).ToArray();

        File.WriteAllText(_manifestFilePath, JsonSerializer.Serialize(manifest, JsonOptions));
        File.WriteAllText(_recordsCacheFilePath, JsonSerializer.Serialize(cacheRecords, JsonOptions));
    }

    private DataQueryCacheManifest BuildCurrentManifest()
    {
        return BuildCurrentManifest(_config, _dataDirectoryPath);
    }

    private static DataQueryCacheManifest BuildCurrentManifest(DataQueryConfig config, string dataDirectoryPath)
    {
        var manifest = new DataQueryCacheManifest
        {
            EmbeddingEnabled = config.Embedding.Enabled,
            EmbeddingModel = config.Embedding.Model,
            EmbeddingEndpoint = config.Embedding.Endpoint,
            MaxTextLengthPerRecord = config.Embedding.MaxTextLengthPerRecord
        };

        if (!Directory.Exists(dataDirectoryPath))
        {
            return manifest;
        }

        foreach (var filePath in Directory.GetFiles(dataDirectoryPath, "*.csv", System.IO.SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileInfo = new FileInfo(filePath);
            manifest.DataFiles.Add(new DataFileSignature
            {
                Name = fileInfo.Name,
                Length = fileInfo.Length,
                LastWriteTimeUtcTicks = fileInfo.LastWriteTimeUtc.Ticks
            });
        }

        return manifest;
    }

    private static bool ManifestEquals(DataQueryCacheManifest left, DataQueryCacheManifest right)
    {
        if (left.EmbeddingEnabled != right.EmbeddingEnabled ||
            !string.Equals(left.EmbeddingModel, right.EmbeddingModel, StringComparison.Ordinal) ||
            !string.Equals(left.EmbeddingEndpoint, right.EmbeddingEndpoint, StringComparison.OrdinalIgnoreCase) ||
            left.MaxTextLengthPerRecord != right.MaxTextLengthPerRecord ||
            left.DataFiles.Count != right.DataFiles.Count)
        {
            return false;
        }

        for (var i = 0; i < left.DataFiles.Count; i++)
        {
            var l = left.DataFiles[i];
            var r = right.DataFiles[i];
            if (!string.Equals(l.Name, r.Name, StringComparison.OrdinalIgnoreCase) ||
                l.Length != r.Length ||
                l.LastWriteTimeUtcTicks != r.LastWriteTimeUtcTicks)
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> NormalizeHeaders(string[]? headers)
    {
        if (headers == null || headers.Length == 0)
        {
            return [];
        }

        var normalized = new List<string>(headers.Length);
        for (var i = 0; i < headers.Length; i++)
        {
            var header = headers[i]?.Trim();
            normalized.Add(string.IsNullOrWhiteSpace(header) ? $"Column{i + 1}" : header);
        }

        return normalized;
    }

    private static string BuildSearchText(string sourceFile, IReadOnlyDictionary<string, string> fields)
    {
        var builder = new StringBuilder();
        builder.Append("source_file: ").Append(sourceFile);
        foreach (var pair in fields)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            builder.Append(" | ").Append(pair.Key).Append(": ").Append(pair.Value);
        }

        return builder.ToString();
    }

    private string TrimForEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var limit = Math.Max(256, _config.Embedding.MaxTextLengthPerRecord);
        return text.Length <= limit ? text : text[..limit];
    }

    private static string BuildEmbeddingText(string query)
    {
        return $"query: {query}";
    }

    private static string NormalizeText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || char.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.OtherLetter)
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder.ToString();
    }

    private static HashSet<string> Tokenize(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            tokens.Add(token);
            if (token.Length <= 1)
            {
                continue;
            }

            for (var i = 0; i < token.Length - 1; i++)
            {
                tokens.Add(token.Substring(i, 2));
            }
        }

        return tokens;
    }

    private static double CalculateLexicalScore(string normalizedQuery, HashSet<string> queryTokens, IndexedRecord record)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var overlap = queryTokens.Count(token => record.Tokens.Contains(token));
        if (overlap == 0 && !record.NormalizedSearchText.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        var union = queryTokens.Count + record.Tokens.Count - overlap;
        var jaccard = union == 0 ? 0 : (double)overlap / union;
        var containsBoost = record.NormalizedSearchText.Contains(normalizedQuery, StringComparison.Ordinal) ? 0.35 : 0;
        var fileBoost = record.SourceFile.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ? 0.1 : 0;
        return jaccard + containsBoost + fileBoost;
    }

    private static DataQueryResult ToResult(IndexedRecord record, double score)
    {
        return new DataQueryResult
        {
            SourceFile = record.SourceFile,
            RowNumber = record.RowNumber,
            Similarity = Math.Round(score, 6),
            Fields = new Dictionary<string, string>(record.Fields, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static DataQueryResponse CreateResponse(
        string query,
        int normalizedTopN,
        double minSimilarity,
        IReadOnlyList<IndexedRecord> filteredRecords,
        IReadOnlyCollection<string>? sourceFiles,
        IReadOnlyList<DataQueryResult> results,
        int candidateCount,
        string searchMode)
    {
        var filteredSourceFiles = sourceFiles?
            .Where(sourceFile => !string.IsNullOrWhiteSpace(sourceFile))
            .Select(sourceFile => sourceFile.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(sourceFile => sourceFile, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        var matchedSourceFiles = results
            .Select(result => result.SourceFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(sourceFile => sourceFile, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DataQueryResponse
        {
            Query = query,
            RequestedTopN = normalizedTopN,
            MinSimilarity = minSimilarity,
            ReturnedCount = results.Count,
            CandidateCount = candidateCount,
            TotalAvailableRecords = filteredRecords.Count,
            SearchMode = searchMode,
            FilteredSourceFiles = filteredSourceFiles,
            MatchedSourceFiles = matchedSourceFiles,
            Results = results
        };
    }

    private static double NormalizeMinSimilarity(double? minSimilarity)
    {
        if (!minSimilarity.HasValue || double.IsNaN(minSimilarity.Value) || double.IsInfinity(minSimilarity.Value))
        {
            return 0;
        }

        return Math.Clamp(minSimilarity.Value, 0, 1);
    }

    private static Encoding DetectEncoding(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
        var utf8Text = utf8.GetString(bytes);
        if (!utf8Text.Contains('\uFFFD'))
        {
            return utf8;
        }

        return Encoding.GetEncoding("GB18030");
    }

    private static string ResolveDirectoryPath(string configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? "." : configuredPath;
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var basePath = AppContext.BaseDirectory;
        var candidate = Path.GetFullPath(Path.Combine(basePath, path));
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    private static IndexedRecord CreateIndexedRecord(string sourceFile, int rowNumber, Dictionary<string, string> fields, string searchText, string? cacheKey = null)
    {
        var normalizedSearchText = NormalizeText(searchText);
        return new IndexedRecord
        {
            CacheKey = cacheKey ?? $"{sourceFile}#{rowNumber}",
            SourceFile = sourceFile,
            RowNumber = rowNumber,
            Fields = new Dictionary<string, string>(fields, StringComparer.OrdinalIgnoreCase),
            SearchText = searchText,
            NormalizedSearchText = normalizedSearchText,
            Tokens = Tokenize(normalizedSearchText)
        };
    }

    private IVectorDB LoadVectorDbFromCache()
    {
        return LiteVectorDBV2.Load(_vectorIndexDirectoryPath, forUnits: true, freeVector: false);
    }

    private static void CompactManagedMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private void DisposeVectorDb()
    {
        _vectorDb = null;
    }

    private sealed class IndexedRecord
    {
        public string CacheKey { get; set; } = string.Empty;

        public string SourceFile { get; set; } = string.Empty;

        public int RowNumber { get; set; }

        public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public string SearchText { get; set; } = string.Empty;

        public string NormalizedSearchText { get; set; } = string.Empty;

        public HashSet<string> Tokens { get; set; } = new(StringComparer.Ordinal);
    }
}
