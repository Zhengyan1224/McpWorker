using System.Net.Http.Headers;
using System.Text.Json;
using LLama;
using LLama.Common;
using Zhengyan.KnowledgeBase;

namespace Zhengyan.KBServer.Implements;

/// <summary>
/// Represents a request to the LM Studio embedding service.
/// </summary>
class EmbeddingRequest
{
    /// <summary>
    /// The input text to be embedded.
    /// </summary>
    required public string input { get; set; }

    /// <summary>
    /// The model to use for generating embeddings.
    /// </summary>
    required public string model { get; set; }
}

/// <summary>
/// Represents a single embedding datum in the response.
/// </summary>
class EmbeddingData
{
    /// <summary>
    /// The object type, typically "embedding".
    /// </summary>
    required public string @object { get; set; }

    /// <summary>
    /// The embedding vector.
    /// </summary>
    required public List<float> embedding { get; set; }

    /// <summary>
    /// The index of this embedding in the response.
    /// </summary>
    required public int index { get; set; }
}

/// <summary>
/// Represents the response from the LM Studio embedding service.
/// </summary>
class EmbeddingResponse
{
    /// <summary>
    /// The object type, typically "list".
    /// </summary>
    required public string @object { get; set; }

    /// <summary>
    /// The list of embedding data.
    /// </summary>
    required public List<EmbeddingData> data { get; set; }

    /// <summary>
    /// The model used to generate the embeddings.
    /// </summary>
    required public string model { get; set; }

    /// <summary>
    /// Usage statistics for the request.
    /// </summary>
    required public Usage usage { get; set; }
}

/// <summary>
/// Represents usage statistics for an embedding request.
/// </summary>
class Usage
{
    /// <summary>
    /// The number of tokens in the prompt.
    /// </summary>
    public int prompt_tokens { get; set; }

    /// <summary>
    /// The total number of tokens used.
    /// </summary>
    public int total_tokens { get; set; }
}


public class LmStudioTextEmbedder : ITextEmbedder
{
    private readonly HttpClient _httpClient;
    public string Url { get; set; }
    public string Model { get; set; }
    private string apiKey;
    public string ApiKey
    {
        get => apiKey;
        set
        {
            apiKey = value;
            if (!string.IsNullOrEmpty(value))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", value);
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }
    }

    public LmStudioTextEmbedder(string url, string model, string apiKey = null)
    {
        _httpClient = new HttpClient();
        if (string.IsNullOrEmpty(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        if (string.IsNullOrEmpty(model))
        {
            throw new ArgumentException("Model cannot be null or empty.", nameof(model));
        }

        Url = url;
        Model = model;
        ApiKey = apiKey;
    }

    public float[] Embedding(string text)
    {
        EmbeddingRequest er = new()
        {
            input = text,
            model = Model
        };
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Content = new StringContent(JsonSerializer.Serialize(er));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage httpResponse = _httpClient.Send(request);
        httpResponse.EnsureSuccessStatusCode();
        var responseBody = httpResponse.Content.ReadAsStringAsync();
        responseBody.Wait();
        string responseJson = responseBody.Result;
        EmbeddingResponse response = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);
        float[] vector = response.data.First().embedding.ToArray();
        return vector;
    }

    public async Task<float[]> EmbeddingAsync(string text)
    {
        EmbeddingRequest er = new()
        {
            input = text,
            model = Model
        };
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Url);
        request.Content = new StringContent(JsonSerializer.Serialize(er));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        HttpResponseMessage httpResponse = await _httpClient.SendAsync(request);
        httpResponse.EnsureSuccessStatusCode();
        var responseJson = await httpResponse.Content.ReadAsStringAsync();
        EmbeddingResponse response = JsonSerializer.Deserialize<EmbeddingResponse>(responseJson);
        float[] vector = response.data.First().embedding.ToArray();
        return vector;
    }
}