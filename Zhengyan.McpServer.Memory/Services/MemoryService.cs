using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Serilog;
using Zhengyan.McpServer.Memory.Config;
using Zhengyan.McpServer.Memory.Models;
using Zhengyan.VectorDB;

namespace Zhengyan.McpServer.Memory.Services;

public class MemoryService : IMemoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private readonly MemoryConfig _config;
    private readonly string _storageDirectoryPath;
    private readonly string _recordsFilePath;
    private readonly string _manifestFilePath;
    private readonly string _vectorIndexDirectoryPath;
    private readonly HttpClient _embeddingHttpClient;

    private List<IndexedMemory> _memories = [];
    private Dictionary<string, IndexedMemory> _memoryLookup = new(StringComparer.OrdinalIgnoreCase);
    private bool _loaded;
    private IVectorDB? _vectorDb;

    public MemoryService(MemoryConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _storageDirectoryPath = ResolveDirectoryPath(config.StorageDirectoryPath);
        _recordsFilePath = Path.Combine(_storageDirectoryPath, "records.json");
        _manifestFilePath = Path.Combine(_storageDirectoryPath, "index_manifest.json");
        _vectorIndexDirectoryPath = Path.Combine(_storageDirectoryPath, "vector_index");

        Directory.CreateDirectory(_storageDirectoryPath);
        _embeddingHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, _config.Embedding.TimeoutSeconds))
        };
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await LoadUnsafeAsync(forceReload: false, cancellationToken);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<RememberMemoryResult> RememberAsync(
        string content,
        string? scope = null,
        IReadOnlyCollection<string>? tags = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? summary = null,
        double? importance = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Memory content cannot be empty.", nameof(content));
        }

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await LoadUnsafeAsync(forceReload: false, cancellationToken);

            var normalizedScope = NormalizeScope(scope);
            var normalizedTags = NormalizeTags(tags);
            var normalizedMetadata = NormalizeMetadata(metadata);
            var now = DateTime.UtcNow;
            var normalizedContent = NormalizeText(content);

            var existing = _memories.FirstOrDefault(memory =>
                string.Equals(memory.Record.Scope, normalizedScope, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(memory.NormalizedContent, normalizedContent, StringComparison.Ordinal));

            var created = false;
            MemoryRecord record;
            if (existing != null)
            {
                record = existing.Record;
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    record.Summary = summary.Trim();
                }

                if (importance.HasValue && !double.IsNaN(importance.Value) && !double.IsInfinity(importance.Value))
                {
                    record.Importance = Math.Clamp(importance.Value, 0, 1);
                }

                record.Tags = MergeTags(record.Tags, normalizedTags);
                record.Metadata = MergeMetadata(record.Metadata, normalizedMetadata);
                record.UpdatedAtUtc = now;
                RefreshIndexedMemory(existing);
            }
            else
            {
                record = new MemoryRecord
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Scope = normalizedScope,
                    Content = content.Trim(),
                    Summary = summary?.Trim() ?? string.Empty,
                    Tags = normalizedTags.ToList(),
                    Metadata = new Dictionary<string, string>(normalizedMetadata, StringComparer.OrdinalIgnoreCase),
                    Importance = importance.HasValue && !double.IsNaN(importance.Value) && !double.IsInfinity(importance.Value)
                        ? Math.Clamp(importance.Value, 0, 1)
                        : 0.5,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                var indexedMemory = CreateIndexedMemory(record);
                _memories.Add(indexedMemory);
                _memoryLookup[record.Id] = indexedMemory;
                created = true;
            }

            SaveRecordsUnsafe();
            await RefreshVectorIndexUnsafeAsync(cancellationToken);

            return new RememberMemoryResult
            {
                Created = created,
                Message = created
                    ? "Memory stored successfully."
                    : "An equivalent memory already existed in the same scope. The existing memory was refreshed instead.",
                VectorSearchAvailable = _vectorDb != null,
                SearchMode = GetCurrentSearchMode(),
                Memory = CloneRecord(record)
            };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<MemoryRecallResponse> RecallAsync(
        string query,
        int? topN = null,
        string? scope = null,
        IReadOnlyCollection<string>? tags = null,
        double? minSimilarity = null,
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await LoadUnsafeAsync(forceReload: false, cancellationToken);

            var normalizedScope = NormalizeScope(scope, defaultScope: string.Empty);
            var normalizedTags = NormalizeTags(tags);
            var normalizedTopN = NormalizeTopN(topN);
            var normalizedMinSimilarity = NormalizeMinSimilarity(minSimilarity);

            var filteredMemories = FilterMemories(normalizedScope, normalizedTags);
            if (string.IsNullOrWhiteSpace(query))
            {
                return CreateRecallResponse(query, normalizedScope, normalizedTags, normalizedTopN, normalizedMinSimilarity, filteredMemories.Count, 0, "none", []);
            }

            if (filteredMemories.Count == 0)
            {
                return CreateRecallResponse(query, normalizedScope, normalizedTags, normalizedTopN, normalizedMinSimilarity, 0, 0, GetCurrentSearchMode(), []);
            }

            var normalizedQuery = NormalizeText(query);
            var queryTokens = Tokenize(normalizedQuery);
            MemoryRecallResponse response;

            if (_vectorDb != null)
            {
                try
                {
                    var queryEmbedding = (await RequestEmbeddingsAsync([TrimForEmbedding(BuildSearchText(query, string.Empty, [], new Dictionary<string, string>(), string.Empty))], cancellationToken)).FirstOrDefault();
                    if (queryEmbedding != null)
                    {
                        response = QueryWithVectorIndex(query, normalizedScope, normalizedTags, normalizedTopN, normalizedMinSimilarity, normalizedQuery, queryTokens, filteredMemories, queryEmbedding);
                        TouchRecalledMemories(response.Results);
                        return response;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to create query embedding. Falling back to lexical search for this request.");
                }
            }

            response = QueryLexically(query, normalizedScope, normalizedTags, normalizedTopN, normalizedMinSimilarity, normalizedQuery, queryTokens, filteredMemories);
            TouchRecalledMemories(response.Results);
            return response;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<IReadOnlyList<MemoryRecord>> ListAsync(
        string? scope = null,
        IReadOnlyCollection<string>? tags = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await LoadUnsafeAsync(forceReload: false, cancellationToken);

            var normalizedScope = NormalizeScope(scope, defaultScope: string.Empty);
            var normalizedTags = NormalizeTags(tags);
            var normalizedLimit = NormalizeListLimit(limit);

            return FilterMemories(normalizedScope, normalizedTags)
                .OrderByDescending(memory => memory.Record.UpdatedAtUtc)
                .ThenByDescending(memory => memory.Record.CreatedAtUtc)
                .Take(normalizedLimit)
                .Select(memory => CloneRecord(memory.Record))
                .ToArray();
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<ForgetMemoryResult> ForgetAsync(string memoryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(memoryId))
        {
            throw new ArgumentException("Memory ID cannot be empty.", nameof(memoryId));
        }

        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await LoadUnsafeAsync(forceReload: false, cancellationToken);

            if (!_memoryLookup.TryGetValue(memoryId.Trim(), out var memory))
            {
                return new ForgetMemoryResult
                {
                    Success = false,
                    MemoryId = memoryId.Trim(),
                    RemainingCount = _memories.Count,
                    VectorSearchAvailable = _vectorDb != null,
                    SearchMode = GetCurrentSearchMode(),
                    Message = "Memory not found."
                };
            }

            _memories.Remove(memory);
            _memoryLookup.Remove(memory.Record.Id);
            SaveRecordsUnsafe();
            await RefreshVectorIndexUnsafeAsync(cancellationToken);

            return new ForgetMemoryResult
            {
                Success = true,
                MemoryId = memoryId.Trim(),
                RemainingCount = _memories.Count,
                VectorSearchAvailable = _vectorDb != null,
                SearchMode = GetCurrentSearchMode(),
                Message = "Memory deleted successfully."
            };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task<MemoryIndexRebuildResult> RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            await LoadUnsafeAsync(forceReload: true, cancellationToken);
            await RefreshVectorIndexUnsafeAsync(cancellationToken, forceRebuild: true);

            return new MemoryIndexRebuildResult
            {
                Success = true,
                MemoryCount = _memories.Count,
                VectorSearchAvailable = _vectorDb != null,
                SearchMode = GetCurrentSearchMode(),
                Message = _vectorDb != null
                    ? "Memory vector index rebuilt successfully."
                    : "Memory index rebuild completed. Vector search is unavailable, lexical search will be used."
            };
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task LoadUnsafeAsync(bool forceReload, CancellationToken cancellationToken)
    {
        if (_loaded && !forceReload)
        {
            return;
        }

        Directory.CreateDirectory(_storageDirectoryPath);
        _memories = LoadRecordsFromDisk();
        _memoryLookup = _memories.ToDictionary(memory => memory.Record.Id, StringComparer.OrdinalIgnoreCase);
        DisposeVectorDb();

        if (ShouldUseVectorSearch())
        {
            if (TryLoadVectorDbFromCacheUnsafe())
            {
                Log.Information("MemoryService loaded {Count} memories and a compatible vector index from {StorageDirectoryPath}.", _memories.Count, _storageDirectoryPath);
            }
            else
            {
                await RefreshVectorIndexUnsafeAsync(cancellationToken, forceRebuild: true);
                Log.Information("MemoryService loaded {Count} memories from {StorageDirectoryPath}. Vector index rebuilt={Rebuilt}.", _memories.Count, _storageDirectoryPath, _vectorDb != null);
            }
        }
        else
        {
            Log.Information("MemoryService loaded {Count} memories from {StorageDirectoryPath}. SearchMode={SearchMode}", _memories.Count, _storageDirectoryPath, GetCurrentSearchMode());
        }

        _loaded = true;
    }

    private List<IndexedMemory> LoadRecordsFromDisk()
    {
        if (!File.Exists(_recordsFilePath))
        {
            return [];
        }

        try
        {
            var records = JsonSerializer.Deserialize<List<MemoryRecord>>(File.ReadAllText(_recordsFilePath), JsonOptions) ?? [];
            return records.Select(CreateIndexedMemory).ToList();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load memory records from {RecordsFilePath}. Starting with an empty store.", _recordsFilePath);
            return [];
        }
    }

    private void SaveRecordsUnsafe()
    {
        Directory.CreateDirectory(_storageDirectoryPath);
        var records = _memories
            .Select(memory => memory.Record)
            .OrderBy(memory => memory.CreatedAtUtc)
            .ThenBy(memory => memory.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        File.WriteAllText(_recordsFilePath, JsonSerializer.Serialize(records, JsonOptions));
    }

    private async Task RefreshVectorIndexUnsafeAsync(CancellationToken cancellationToken, bool forceRebuild = false)
    {
        if (!ShouldUseVectorSearch())
        {
            DisposeVectorDb();
            return;
        }

        if (!forceRebuild && TryLoadVectorDbFromCacheUnsafe())
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_vectorIndexDirectoryPath);
            var embeddings = await BuildEmbeddingsForMemoriesAsync(cancellationToken);
            if (embeddings.Count != _memories.Count)
            {
                throw new InvalidOperationException($"Embedding count mismatch. expected={_memories.Count}, actual={embeddings.Count}");
            }

            var items = new List<(float[], byte[])>(_memories.Count);
            for (var i = 0; i < _memories.Count; i++)
            {
                items.Add((embeddings[i], Encoding.UTF8.GetBytes(_memories[i].Record.Id)));
            }

            DisposeVectorDb();
            LiteVectorDBV2.CreateNew(_vectorIndexDirectoryPath, forUnits: true, freeVector: false)
                .AddItems(items)
                .Save();

            _vectorDb = LiteVectorDBV2.Load(_vectorIndexDirectoryPath, forUnits: true, freeVector: false);
            SaveManifestUnsafe();
        }
        catch (Exception ex)
        {
            DisposeVectorDb();
            Log.Warning(ex, "Failed to rebuild memory vector index. Lexical retrieval will still work.");
        }
    }

    private async Task<List<float[]>> BuildEmbeddingsForMemoriesAsync(CancellationToken cancellationToken)
    {
        var texts = _memories
            .Select(memory => TrimForEmbedding(memory.SearchText))
            .ToArray();

        return await RequestEmbeddingsAsync(texts, cancellationToken);
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

    private bool TryLoadVectorDbFromCacheUnsafe()
    {
        if (!File.Exists(_manifestFilePath))
        {
            return false;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<MemoryIndexManifest>(File.ReadAllText(_manifestFilePath), JsonOptions);
            if (manifest == null || !ManifestEquals(manifest, BuildCurrentManifest()))
            {
                return false;
            }

            var requiredFiles = new[]
            {
                Path.Combine(_vectorIndexDirectoryPath, LiteVectorDBV2.GraphFileName),
                Path.Combine(_vectorIndexDirectoryPath, LiteVectorDBV2.IndexFileName),
                Path.Combine(_vectorIndexDirectoryPath, LiteVectorDBV2.VectorFileName),
                Path.Combine(_vectorIndexDirectoryPath, LocalDiskDataStorage.DataFileName),
                Path.Combine(_vectorIndexDirectoryPath, LocalDiskDataStorage.DataMapFileName)
            };

            if (requiredFiles.Any(filePath => !File.Exists(filePath)))
            {
                return false;
            }

            _vectorDb = LiteVectorDBV2.Load(_vectorIndexDirectoryPath, forUnits: true, freeVector: false);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load cached memory vector index from {VectorIndexDirectoryPath}.", _vectorIndexDirectoryPath);
            DisposeVectorDb();
            return false;
        }
    }

    private void SaveManifestUnsafe()
    {
        File.WriteAllText(_manifestFilePath, JsonSerializer.Serialize(BuildCurrentManifest(), JsonOptions));
    }

    private MemoryIndexManifest BuildCurrentManifest()
    {
        return new MemoryIndexManifest
        {
            EmbeddingEnabled = _config.Embedding.Enabled,
            EmbeddingModel = _config.Embedding.Model,
            EmbeddingEndpoint = _config.Embedding.Endpoint,
            MaxTextLengthPerMemory = _config.Embedding.MaxTextLengthPerMemory,
            MemoryCount = _memories.Count,
            RecordFingerprint = ComputeFingerprint(_memories)
        };
    }

    private static string ComputeFingerprint(IReadOnlyList<IndexedMemory> memories)
    {
        var payload = memories
            .Select(memory => new
            {
                memory.Record.Id,
                memory.Record.Scope,
                memory.Record.Content,
                memory.Record.Summary,
                Tags = memory.Record.Tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase).ToArray(),
                Metadata = memory.Record.Metadata
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(pair => new { pair.Key, pair.Value })
                    .ToArray()
            })
            .OrderBy(memory => memory.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool ManifestEquals(MemoryIndexManifest left, MemoryIndexManifest right)
    {
        return left.EmbeddingEnabled == right.EmbeddingEnabled &&
               string.Equals(left.EmbeddingModel, right.EmbeddingModel, StringComparison.Ordinal) &&
               string.Equals(left.EmbeddingEndpoint, right.EmbeddingEndpoint, StringComparison.OrdinalIgnoreCase) &&
               left.MaxTextLengthPerMemory == right.MaxTextLengthPerMemory &&
               left.MemoryCount == right.MemoryCount &&
               string.Equals(left.RecordFingerprint, right.RecordFingerprint, StringComparison.Ordinal);
    }

    private MemoryRecallResponse QueryWithVectorIndex(
        string query,
        string scope,
        IReadOnlyList<string> tags,
        int topN,
        double minSimilarity,
        string normalizedQuery,
        HashSet<string> queryTokens,
        IReadOnlyList<IndexedMemory> filteredMemories,
        float[] queryEmbedding)
    {
        var filteredIds = filteredMemories.Select(memory => memory.Record.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateCount = filteredMemories.Count == _memories.Count
            ? Math.Min(Math.Max(topN * Math.Max(_config.ApproximateSearchCandidateMultiplier, 1), topN), _memories.Count)
            : filteredMemories.Count;
        var candidates = new List<(IndexedMemory Memory, double Score)>();

        foreach (var result in _vectorDb!.SearchTopK(queryEmbedding, candidateCount))
        {
            var memoryId = Encoding.UTF8.GetString(result.Content);
            if (!filteredIds.Contains(memoryId))
            {
                continue;
            }

            if (!_memoryLookup.TryGetValue(memoryId, out var memory))
            {
                continue;
            }

            var lexicalScore = CalculateLexicalScore(normalizedQuery, queryTokens, memory);
            var semanticScore = Math.Max(0d, 1d - result.Distance);
            var finalScore = semanticScore * 0.82 + lexicalScore * 0.12 + memory.Record.Importance * 0.06;
            candidates.Add((memory, finalScore));
        }

        var results = candidates
            .Where(item => item.Score >= minSimilarity)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Memory.Record.UpdatedAtUtc)
            .Take(topN)
            .Select(item => ToRecallResult(item.Memory.Record, item.Score))
            .ToArray();

        return CreateRecallResponse(query, scope, tags, topN, minSimilarity, filteredMemories.Count, candidates.Count, "vector", results);
    }

    private MemoryRecallResponse QueryLexically(
        string query,
        string scope,
        IReadOnlyList<string> tags,
        int topN,
        double minSimilarity,
        string normalizedQuery,
        HashSet<string> queryTokens,
        IReadOnlyList<IndexedMemory> filteredMemories)
    {
        var candidates = filteredMemories
            .Select(memory => (Memory: memory, Score: CalculateLexicalScore(normalizedQuery, queryTokens, memory) * 0.94 + memory.Record.Importance * 0.06))
            .Where(item => item.Score > 0)
            .ToArray();

        var results = candidates
            .Where(item => item.Score >= minSimilarity)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Memory.Record.UpdatedAtUtc)
            .Take(topN)
            .Select(item => ToRecallResult(item.Memory.Record, item.Score))
            .ToArray();

        return CreateRecallResponse(query, scope, tags, topN, minSimilarity, filteredMemories.Count, candidates.Length, "lexical", results);
    }

    private IReadOnlyList<IndexedMemory> FilterMemories(string scope, IReadOnlyList<string> tags)
    {
        IEnumerable<IndexedMemory> query = _memories;
        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(memory => string.Equals(memory.Record.Scope, scope, StringComparison.OrdinalIgnoreCase));
        }

        if (tags.Count > 0)
        {
            var tagSet = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
            query = query.Where(memory => tagSet.All(tag => memory.Record.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        return query.ToArray();
    }

    private void TouchRecalledMemories(IReadOnlyList<MemoryRecallResult> results)
    {
        if (results.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var result in results)
        {
            if (_memoryLookup.TryGetValue(result.Id, out var memory))
            {
                memory.Record.AccessCount++;
                memory.Record.LastAccessedAtUtc = now;
                result.AccessCount = memory.Record.AccessCount;
                result.LastAccessedAtUtc = memory.Record.LastAccessedAtUtc;
            }
        }

        SaveRecordsUnsafe();
    }

    private MemoryRecallResponse CreateRecallResponse(
        string query,
        string scope,
        IReadOnlyList<string> tags,
        int topN,
        double minSimilarity,
        int totalAvailableMemories,
        int candidateCount,
        string searchMode,
        IReadOnlyList<MemoryRecallResult> results)
    {
        return new MemoryRecallResponse
        {
            Query = query,
            Scope = scope,
            Tags = tags,
            RequestedTopN = topN,
            MinSimilarity = minSimilarity,
            ReturnedCount = results.Count,
            CandidateCount = candidateCount,
            TotalAvailableMemories = totalAvailableMemories,
            SearchMode = searchMode,
            Results = results
        };
    }

    private static MemoryRecallResult ToRecallResult(MemoryRecord record, double score)
    {
        return new MemoryRecallResult
        {
            Id = record.Id,
            Scope = record.Scope,
            Similarity = Math.Round(score, 6),
            Content = record.Content,
            Summary = record.Summary,
            Tags = [.. record.Tags],
            Metadata = new Dictionary<string, string>(record.Metadata, StringComparer.OrdinalIgnoreCase),
            Importance = record.Importance,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            LastAccessedAtUtc = record.LastAccessedAtUtc,
            AccessCount = record.AccessCount
        };
    }

    private static double CalculateLexicalScore(string normalizedQuery, HashSet<string> queryTokens, IndexedMemory memory)
    {
        if (queryTokens.Count == 0)
        {
            return 0;
        }

        var overlap = queryTokens.Count(token => memory.Tokens.Contains(token));
        if (overlap == 0 && !memory.NormalizedSearchText.Contains(normalizedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        var union = queryTokens.Count + memory.Tokens.Count - overlap;
        var jaccard = union == 0 ? 0 : (double)overlap / union;
        var containsBoost = memory.NormalizedSearchText.Contains(normalizedQuery, StringComparison.Ordinal) ? 0.35 : 0;
        var scopeBoost = memory.Record.Scope.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ? 0.1 : 0;
        return jaccard + containsBoost + scopeBoost;
    }

    private static IndexedMemory CreateIndexedMemory(MemoryRecord record)
    {
        record.Scope = NormalizeScope(record.Scope);
        record.Tags = NormalizeTags(record.Tags).ToList();
        record.Metadata = NormalizeMetadata(record.Metadata);
        record.Content = record.Content?.Trim() ?? string.Empty;
        record.Summary = record.Summary?.Trim() ?? string.Empty;
        record.Importance = double.IsNaN(record.Importance) || double.IsInfinity(record.Importance)
            ? 0.5
            : Math.Clamp(record.Importance, 0, 1);

        var searchText = BuildSearchText(record.Content, record.Scope, record.Tags, record.Metadata, record.Summary);
        var normalizedSearchText = NormalizeText(searchText);
        return new IndexedMemory
        {
            Record = record,
            SearchText = searchText,
            NormalizedSearchText = normalizedSearchText,
            NormalizedContent = NormalizeText(record.Content),
            Tokens = Tokenize(normalizedSearchText)
        };
    }

    private static void RefreshIndexedMemory(IndexedMemory memory)
    {
        var refreshed = CreateIndexedMemory(memory.Record);
        memory.SearchText = refreshed.SearchText;
        memory.NormalizedSearchText = refreshed.NormalizedSearchText;
        memory.NormalizedContent = refreshed.NormalizedContent;
        memory.Tokens = refreshed.Tokens;
    }

    private static string BuildSearchText(string content, string scope, IReadOnlyList<string> tags, IReadOnlyDictionary<string, string> metadata, string summary)
    {
        var builder = new StringBuilder();
        builder.Append("scope: ").Append(scope);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append(" | summary: ").Append(summary);
        }

        if (tags.Count > 0)
        {
            builder.Append(" | tags: ").Append(string.Join(", ", tags));
        }

        foreach (var pair in metadata.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            builder.Append(" | ").Append(pair.Key).Append(": ").Append(pair.Value);
        }

        builder.Append(" | content: ").Append(content);
        return builder.ToString();
    }

    private string TrimForEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var limit = Math.Max(256, _config.Embedding.MaxTextLengthPerMemory);
        return text.Length <= limit ? text : text[..limit];
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

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

    private static string NormalizeScope(string? scope, string defaultScope = "default")
    {
        return string.IsNullOrWhiteSpace(scope) ? defaultScope : scope.Trim();
    }

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags == null)
        {
            return [];
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static Dictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (metadata == null)
        {
            return normalized;
        }

        foreach (var pair in metadata)
        {
            var key = pair.Key?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            normalized[key] = pair.Value?.Trim() ?? string.Empty;
        }

        return normalized;
    }

    private static List<string> MergeTags(IEnumerable<string> left, IEnumerable<string> right)
    {
        return left
            .Concat(right)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        var merged = new Dictionary<string, string>(left, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in right)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private int NormalizeTopN(int? topN)
    {
        var normalized = topN.GetValueOrDefault(_config.DefaultTopN);
        if (normalized <= 0)
        {
            normalized = _config.DefaultTopN;
        }

        return Math.Min(normalized, Math.Max(1, _config.MaxTopN));
    }

    private int NormalizeListLimit(int? limit)
    {
        var normalized = limit.GetValueOrDefault(_config.DefaultListLimit);
        if (normalized <= 0)
        {
            normalized = _config.DefaultListLimit;
        }

        return Math.Min(normalized, Math.Max(1, _config.MaxListLimit));
    }

    private static double NormalizeMinSimilarity(double? minSimilarity)
    {
        if (!minSimilarity.HasValue || double.IsNaN(minSimilarity.Value) || double.IsInfinity(minSimilarity.Value))
        {
            return 0;
        }

        return Math.Clamp(minSimilarity.Value, 0, 1);
    }

    private bool ShouldUseVectorSearch()
    {
        return _config.Embedding.Enabled && _memories.Count > 0 && _memories.Count <= Math.Max(1, _config.MaxMemoriesForEmbedding);
    }

    private string GetCurrentSearchMode()
    {
        return _vectorDb != null ? "vector" : "lexical";
    }

    private void DisposeVectorDb()
    {
        _vectorDb = null;
    }

    private static MemoryRecord CloneRecord(MemoryRecord record)
    {
        return new MemoryRecord
        {
            Id = record.Id,
            Scope = record.Scope,
            Content = record.Content,
            Summary = record.Summary,
            Tags = [.. record.Tags],
            Metadata = new Dictionary<string, string>(record.Metadata, StringComparer.OrdinalIgnoreCase),
            Importance = record.Importance,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            LastAccessedAtUtc = record.LastAccessedAtUtc,
            AccessCount = record.AccessCount
        };
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

    private sealed class IndexedMemory
    {
        public MemoryRecord Record { get; set; } = new();

        public string SearchText { get; set; } = string.Empty;

        public string NormalizedSearchText { get; set; } = string.Empty;

        public string NormalizedContent { get; set; } = string.Empty;

        public HashSet<string> Tokens { get; set; } = new(StringComparer.Ordinal);
    }
}
