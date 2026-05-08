using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using Gradio.Net;
using Gradio.Net.AspNetCore;
using Gradio.Net.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Zhengyan.OpenAIModels;

const string AdditionalPropertiesDetailsStart = "<details class=\"zhengyan-additional-properties\">";
const string AdditionalPropertiesSummary = "<summary>Additional Properties</summary>";
const string AdditionalPropertiesPreStart = "<pre style=\"margin-top:0.75rem;overflow-x:auto;white-space:pre-wrap;\">";
const string BrowserSettingsScript = """
<script>
(() => {
  const storageKey = "zhengyan.chatui.web.settings";
  const labelMap = {
    "Server Endpoint": "ServerEndpoint",
    "API Key": "ApiKey",
    "Model Select": "SelectedModel",
    "Max Completion Tokens": "MaxCompletionTokens",
    "Temperature": "Temperature",
    "Top P": "TopP",
    "Use /v1/responses (off = /v1/chat/completions)": "UseResponsesApi"
  };

  function readSettings() {
    try {
      const raw = window.localStorage.getItem(storageKey);
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  }

  function writeSettings(settings) {
    try {
      window.localStorage.setItem(storageKey, JSON.stringify(settings));
    } catch {
    }
  }

  function applyInitialConfig() {
    const settings = readSettings();
    const config = window.gradio_config;
    if (!config || !Array.isArray(config.components)) {
      return;
    }

    for (const component of config.components) {
      const label = component?.props?.label;
      if (!label || !(label in labelMap)) {
        continue;
      }

      const key = labelMap[label];
      const value = settings[key];
      if (value === undefined || value === null || value === "") {
        continue;
      }

      if (component.type === "checkbox") {
        component.props.value = !!value;
        continue;
      }

      if (component.type === "dropdown") {
        component.props.value = value;
        if (!Array.isArray(component.props.choices) || component.props.choices.length === 0) {
          component.props.choices = [[value, value]];
        } else if (!component.props.choices.some(choice => Array.isArray(choice) && choice[1] === value)) {
          component.props.choices = [...component.props.choices, [value, value]];
        }
        continue;
      }

      component.props.value = value;
    }
  }

  function findLabeledContainer(labelText) {
    const nodes = [...document.querySelectorAll("label, span, p, div")];
    for (const node of nodes) {
      if (node.textContent?.trim() !== labelText) {
        continue;
      }

      const container = node.closest("label, fieldset, .form, .block, .gradio-container") ?? node.parentElement;
      if (container && container.querySelector("input, textarea, select, button")) {
        return container;
      }
    }

    return null;
  }

  function findField(labelText) {
    const container = findLabeledContainer(labelText);
    if (!container) {
      return null;
    }

    if (labelText === "Model Select") {
      return container.querySelector("input, select, button");
    }

    return container.querySelector("input, textarea, select");
  }

  function findButton(buttonText) {
    return [...document.querySelectorAll("button")].find(button => button.textContent?.trim() === buttonText) ?? null;
  }

  function readFieldValue(labelText) {
    const field = findField(labelText);
    if (!field) {
      return null;
    }

    if (field instanceof HTMLInputElement && field.type === "checkbox") {
      return field.checked;
    }

    if (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement || field instanceof HTMLSelectElement) {
      return field.value;
    }

    return field.getAttribute("value") ?? field.textContent?.trim() ?? null;
  }

  function collectSettings() {
    return {
      ServerEndpoint: readFieldValue("Server Endpoint") ?? "http://localhost:9083/mcphost/api/v1",
      ApiKey: readFieldValue("API Key") ?? "",
      SelectedModel: readFieldValue("Model Select") ?? "",
      MaxCompletionTokens: readFieldValue("Max Completion Tokens") ?? "4096",
      Temperature: readFieldValue("Temperature") ?? "0.9",
      TopP: readFieldValue("Top P") ?? "0.9",
      UseResponsesApi: !!readFieldValue("Use /v1/responses (off = /v1/chat/completions)")
    };
  }

  function persistCurrentSettings() {
    writeSettings(collectSettings());
  }

  function bindPersistence() {
    const labels = [
      "Server Endpoint",
      "API Key",
      "Model Select",
      "Max Completion Tokens",
      "Temperature",
      "Top P",
      "Use /v1/responses (off = /v1/chat/completions)"
    ];

    for (const labelText of labels) {
      const field = findField(labelText);
      if (!field || field.dataset.zhengyanBound === "1") {
        continue;
      }

      field.dataset.zhengyanBound = "1";
      field.addEventListener("input", persistCurrentSettings);
      field.addEventListener("change", persistCurrentSettings);
      field.addEventListener("blur", persistCurrentSettings);
    }

    const saveButton = findButton("Save Settings");
    if (saveButton && saveButton.dataset.zhengyanBound !== "1") {
      saveButton.dataset.zhengyanBound = "1";
      saveButton.addEventListener("click", () => {
        window.setTimeout(persistCurrentSettings, 0);
      });
    }
  }

  applyInitialConfig();

  const observer = new MutationObserver(() => bindPersistence());
  observer.observe(document.documentElement, { childList: true, subtree: true });
  window.addEventListener("DOMContentLoaded", () => bindPersistence());
})();
</script>
""";

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddGradio();

var app = builder.Build();
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method)
        || context.Request.Path.StartsWithSegments("/gradio_api", StringComparison.OrdinalIgnoreCase)
        || context.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
        || Path.HasExtension(context.Request.Path.Value))
    {
        await next();
        return;
    }

    var originalBody = context.Response.Body;
    await using var buffer = new MemoryStream();
    context.Response.Body = buffer;

    try
    {
        await next();
        buffer.Position = 0;

        if (context.Response.ContentType?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var reader = new StreamReader(buffer, Encoding.UTF8);
            var html = await reader.ReadToEndAsync();
            html = html.Replace("</head>", BrowserSettingsScript + "\n</head>", StringComparison.OrdinalIgnoreCase);

            var bytes = Encoding.UTF8.GetBytes(html);
            context.Response.ContentLength = bytes.Length;
            context.Response.Body = originalBody;
            await context.Response.Body.WriteAsync(bytes);
            return;
        }

        context.Response.Body = originalBody;
        buffer.Position = 0;
        await buffer.CopyToAsync(context.Response.Body);
    }
    finally
    {
        context.Response.Body = originalBody;
    }
});
app.UseGradio(await CreateBlocks());
app.Run();

static async Task<Blocks> CreateBlocks()
{
    using var blocks = gr.Blocks(analyticsEnabled: false);
    gr.Markdown("# Chat Test UI\n##### Author: Zhengyan");

    var (serverInput, tokenInput, getModelsButton, modelInput, maxTokensInput, temperatureInput, topPInput, useResponsesApiInput, saveSettingsButton, settingsStatus) = InitializeUi();

    _ = getModelsButton.Click(UpdateModels, inputs: [serverInput, tokenInput], outputs: [modelInput]);
    _ = modelInput.Change(ChangeModels, inputs: [serverInput, modelInput], outputs: [modelInput]);
    _ = saveSettingsButton.Click(
        SaveSettings,
        inputs: [serverInput, tokenInput, modelInput, maxTokensInput, temperatureInput, topPInput, useResponsesApiInput],
        outputs: [settingsStatus]);

    AddChatTab(serverInput, tokenInput, modelInput, maxTokensInput, temperatureInput, topPInput, useResponsesApiInput);
    return blocks;
}

static (Textbox serverInput, Textbox tokenInput, Button getModelsButton, Dropdown modelInput, Textbox maxTokensInput, Textbox temperatureInput, Textbox topPInput, Checkbox useResponsesApiInput, Button saveSettingsButton, Markdown settingsStatus) InitializeUi()
{
    Textbox serverInput;
    Textbox tokenInput;
    Button getModelsButton;
    Dropdown modelInput;
    Textbox maxTokensInput;
    Textbox temperatureInput;
    Textbox topPInput;
    Checkbox useResponsesApiInput;
    Button saveSettingsButton;
    Markdown settingsStatus;

    using (gr.Tab("Settings"))
    {
        using (gr.Row())
        {
            serverInput = gr.Textbox("http://localhost:9083/mcphost/api/v1", placeholder: "http(s)://xxxxxxx:xxxx[/xxx]/v1", label: "Server Endpoint");
            tokenInput = gr.Textbox(placeholder: "API Key", label: "API Key", maxLines: 1, type: TextboxType.Password);
        }

        using (gr.Row())
        {
            getModelsButton = gr.Button("Get Models", variant: ButtonVariant.Primary);
            modelInput = gr.Dropdown(choices: [], label: "Model Select", allowCustomValue: true);
        }

        using (gr.Row())
        {
            maxTokensInput = gr.Textbox("4096", label: "Max Completion Tokens", placeholder: "Enter max tokens (e.g. 4096)", lines: 1, type: TextboxType.Text);
            temperatureInput = gr.Textbox("0.9", label: "Temperature", placeholder: "0.9", lines: 1, type: TextboxType.Text);
            topPInput = gr.Textbox("0.9", label: "Top P", placeholder: "0.9", lines: 1, type: TextboxType.Text);
        }

        useResponsesApiInput = gr.Checkbox(value: false, label: "Use /v1/responses (off = /v1/chat/completions)");

        using (gr.Row())
        {
            saveSettingsButton = gr.Button("Save Settings", variant: ButtonVariant.Primary);
            settingsStatus = gr.Markdown(string.Empty);
        }
    }

    return (serverInput, tokenInput, getModelsButton, modelInput, maxTokensInput, temperatureInput, topPInput, useResponsesApiInput, saveSettingsButton, settingsStatus);
}

static void AddChatTab(
    Textbox serverInput,
    Textbox tokenInput,
    Dropdown modelInput,
    Textbox maxTokensInput,
    Textbox temperatureInput,
    Textbox topPInput,
    Checkbox useResponsesApiInput)
{
    using (gr.Tab("Chat"))
    {
        var chatBot = gr.Chatbot(label: "Chat", showCopyButton: true, placeholder: "Chat history", height: 520);
        var userInput = gr.Textbox(label: "Input", placeholder: "Type a message...");
        var imageUrlInput = gr.Textbox(label: "Image URL", placeholder: "https://example.com/demo.jpg or data:image/...");
        var localImageInput = gr.File(
            fileCount: FileCount.Single,
            fileTypes: [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"],
            type: FileType.Filepath,
            label: "Local Image");

        Button sendButton;
        Button regenerateButton;
        Button resetButton;

        using (gr.Row())
        {
            sendButton = gr.Button("Send", variant: ButtonVariant.Primary);
            regenerateButton = gr.Button("Retry", variant: ButtonVariant.Secondary);
            resetButton = gr.Button("Clear", variant: ButtonVariant.Stop);
        }

        sendButton.Click(
            streamingFn: HandleSendMessage,
            inputs: [serverInput, chatBot, userInput, tokenInput, modelInput, maxTokensInput, temperatureInput, topPInput, useResponsesApiInput, imageUrlInput, localImageInput],
            outputs: [userInput, chatBot, imageUrlInput, localImageInput]
        );

        regenerateButton.Click(
            streamingFn: HandleRegenerateMessage,
            inputs: [serverInput, chatBot, tokenInput, modelInput, maxTokensInput, temperatureInput, topPInput, useResponsesApiInput],
            outputs: [userInput, chatBot, imageUrlInput, localImageInput]
        );

        resetButton.Click(
            _ => Task.FromResult(CreateChatOutput(Array.Empty<ChatbotMessagePair>())),
            outputs: [userInput, chatBot, imageUrlInput, localImageInput]
        );
    }
}

static async IAsyncEnumerable<Output> HandleSendMessage(Input input)
{
    var server = Textbox.Payload(input.Data[0]);
    var chatHistory = Chatbot.Payload(input.Data[1]);
    var userMessage = Textbox.Payload(input.Data[2]);
    var token = Textbox.Payload(input.Data[3]);
    var model = Dropdown.Payload(input.Data[4]).SingleOrDefault() ?? throw new Exception("No model selected.");
    var maxTokens = ParseMaxTokens(Textbox.Payload(input.Data[5]));
    var temperature = ParseTemperature(Textbox.Payload(input.Data[6]));
    var topP = ParseTopP(Textbox.Payload(input.Data[7]));
    var useResponsesApi = Checkbox.Payload(input.Data[8]);
    var imageUrl = Textbox.Payload(input.Data[9]);
    var localImagePath = ParseOptionalLocalImagePath(input.Data[10]);
    var chatState = BuildChatStateFromHistory(chatHistory);

    var attachments = BuildAttachments(imageUrl, localImagePath);
    await foreach (var output in ProcessChatMessages(server, token, model, useResponsesApi, chatHistory, chatState, userMessage, attachments, maxTokens, temperature, topP))
    {
        yield return output;
    }
}

static async IAsyncEnumerable<Output> HandleRegenerateMessage(Input input)
{
    var server = Textbox.Payload(input.Data[0]);
    var chatHistory = Chatbot.Payload(input.Data[1]);
    var token = Textbox.Payload(input.Data[2]);
    var model = Dropdown.Payload(input.Data[3]).SingleOrDefault() ?? throw new Exception("No model selected.");
    var maxTokens = ParseMaxTokens(Textbox.Payload(input.Data[4]));
    var temperature = ParseTemperature(Textbox.Payload(input.Data[5]));
    var topP = ParseTopP(Textbox.Payload(input.Data[6]));
    var useResponsesApi = Checkbox.Payload(input.Data[7]);
    var chatState = BuildChatStateFromHistory(chatHistory);

    if (chatState.Count == 0)
    {
        throw new Exception("No chat history available for regeneration.");
    }

    var lastTurn = chatState[^1];
    chatState.RemoveAt(chatState.Count - 1);
    if (chatHistory.Count > 0)
    {
        chatHistory.RemoveAt(chatHistory.Count - 1);
    }

    await foreach (var output in ProcessChatMessages(server, token, model, useResponsesApi, chatHistory, chatState, lastTurn.UserMessage, CloneAttachments(lastTurn.Attachments), maxTokens, temperature, topP))
    {
        yield return output;
    }
}

static async IAsyncEnumerable<Output> ProcessChatMessages(
    string server,
    string token,
    string model,
    bool useResponsesApi,
    IList<ChatbotMessagePair> chatHistory,
    List<WebChatTurn> chatState,
    string message,
    List<WebChatAttachment> attachments,
    int maxTokens,
    float temperature,
    float topP)
{
    if (string.IsNullOrWhiteSpace(message) && attachments.Count == 0)
    {
        yield return CreateChatOutput(chatHistory);
        yield break;
    }

    var turn = new WebChatTurn
    {
        UserMessage = message ?? string.Empty,
        Attachments = CloneAttachments(attachments),
        AssistantMessage = string.Empty
    };

    chatState.Add(turn);
    chatHistory.Add(new ChatbotMessagePair(BuildHumanDisplayText(turn.UserMessage, turn.Attachments), string.Empty));

    var request = new HttpRequestMessage(HttpMethod.Post, $"{server}{GetChatApiPath(useResponsesApi)}");
    request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

    if (!string.IsNullOrWhiteSpace(token))
    {
        Utils.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
    else
    {
        Utils.Client.DefaultRequestHeaders.Authorization = null;
    }

    request.Content = BuildStreamRequestContent(chatState, model, maxTokens, temperature, topP, useResponsesApi);

    using var response = await Utils.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    response.EnsureSuccessStatusCode();

    using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);

    if (useResponsesApi)
    {
        await foreach (var output in ProcessResponsesStreamAsync(reader, turn, chatHistory))
        {
            yield return output;
        }

        yield break;
    }

    var inThink = false;

    while (true)
    {
        var sseEvent = await ReadSseEventAsync(reader);
        if (sseEvent is null)
        {
            yield break;
        }

        if (sseEvent.Data == "[DONE]")
        {
            yield break;
        }

        ChatCompletionChunkResponse? completionResponse;
        try
        {
            completionResponse = JsonSerializer.Deserialize<ChatCompletionChunkResponse>(sseEvent.Data);
        }
        catch (JsonException)
        {
            continue;
        }

        var delta = completionResponse?.choices.FirstOrDefault()?.delta;
        var text = ChatContentTextExtractor.GetText(delta?.content);
        var reasoning = delta?.reasoning_content;
        var additionalProperties = delta?.additional_properties;
        if (!string.IsNullOrEmpty(text))
        {
            if (inThink)
            {
                text = "\n```\n" + text;
                inThink = false;
            }

            if (text == "<think>")
            {
                text = "```\n";
            }
            else if (text == "</think>")
            {
                text = "\n```\n";
            }

            turn.AssistantMessage += text;
            chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
            yield return CreateChatOutput(chatHistory);
        }
        else if (!string.IsNullOrEmpty(reasoning))
        {
            if (!inThink)
            {
                reasoning = "```\n" + reasoning;
            }

            inThink = true;
            turn.AssistantMessage += reasoning;
            chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
            yield return CreateChatOutput(chatHistory);
        }

        if (additionalProperties is { Count: > 0 })
        {
            turn.AssistantAdditionalProperties = FormatAdditionalProperties(additionalProperties);
            chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
            yield return CreateChatOutput(chatHistory);
        }
    }
}

static Output CreateChatOutput(IEnumerable<ChatbotMessagePair> chatHistory)
{
    object? clearedFile = null;
    return gr.Output(string.Empty, chatHistory.ToArray(), string.Empty, clearedFile!);
}

static async Task<Output> UpdateModels(Input input)
{
    var server = Textbox.Payload(input.Data[0]);
    var token = Textbox.Payload(input.Data[1]);
    if (string.IsNullOrWhiteSpace(server))
    {
        throw new Exception("Server URL cannot be empty.");
    }

    if (!string.IsNullOrWhiteSpace(token))
    {
        Utils.Client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }
    else
    {
        Utils.Client.DefaultRequestHeaders.Authorization = null;
    }

    var config = await Utils.Client.GetFromJsonAsync<ConfigModels>($"{server}/models/config");
    if (config?.Models == null || config.Models.Count == 0)
    {
        throw new Exception("Failed to fetch models from the server.");
    }

    Utils.Config = config;
    var models = config.Models.Select(item => item.Name).ToList();
    return gr.Output(gr.Dropdown(choices: models, value: models[config.Current], interactive: true, allowCustomValue: true));
}

static async Task<Output> ChangeModels(Input input)
{
    var server = Textbox.Payload(input.Data[0]);
    var model = Dropdown.Payload(input.Data[1]).Single();

    var models = Utils.Config?.Models?.Select(item => item.Name).ToList();
    if (models == null)
    {
        return gr.Output(gr.Dropdown(choices: [model], value: model, interactive: true, allowCustomValue: true));
    }

    if (string.IsNullOrWhiteSpace(server))
    {
        throw new Exception("Server URL cannot be empty.");
    }

    var index = models.IndexOf(model);
    if (index == -1)
    {
        throw new Exception("Model not found in the list of available models.");
    }

    if (Utils.Config is not null && Utils.Config.Current == index)
    {
        return gr.Output(gr.Dropdown(choices: models, value: model, interactive: true, allowCustomValue: true));
    }

    var response = await Utils.Client.PutAsync($"{server}/models/switch?id={index}", null);
    if (!response.IsSuccessStatusCode)
    {
        gr.Warning("Failed to switch model.");
        await Task.Delay(2000);
        var currentIndex = Utils.Config?.Current ?? 0;
        return gr.Output(gr.Dropdown(choices: models, value: models[currentIndex], interactive: true, allowCustomValue: true));
    }

    (Utils.Config ??= new ConfigModels()).Current = index;
    return gr.Output(gr.Dropdown(choices: models, value: model, interactive: true, allowCustomValue: true));
}

static async IAsyncEnumerable<Output> ProcessResponsesStreamAsync(
    StreamReader reader,
    WebChatTurn turn,
    IList<ChatbotMessagePair> chatHistory)
{
    var inThink = false;

    while (true)
    {
        var sseEvent = await ReadSseEventAsync(reader);
        if (sseEvent is null)
        {
            yield break;
        }

        if (sseEvent.Data == "[DONE]")
        {
            yield break;
        }

        var eventType = sseEvent.EventName ?? TryGetJsonStringProperty(sseEvent.Data, "type");
        switch (eventType)
        {
            case "response.reasoning.delta":
                var reasoningDelta = TryGetJsonStringProperty(sseEvent.Data, "delta");
                if (!string.IsNullOrEmpty(reasoningDelta))
                {
                    if (!inThink)
                    {
                        turn.AssistantMessage += "```\n";
                    }

                    inThink = true;
                    turn.AssistantMessage += reasoningDelta;
                    chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
                    yield return CreateChatOutput(chatHistory);
                }
                break;
            case "response.reasoning.done":
                if (inThink)
                {
                    turn.AssistantMessage += "\n```\n";
                    inThink = false;
                    chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
                    yield return CreateChatOutput(chatHistory);
                }
                break;
            case "response.output_text.delta":
                var delta = TryGetJsonStringProperty(sseEvent.Data, "delta");
                if (!string.IsNullOrEmpty(delta))
                {
                    if (inThink)
                    {
                        turn.AssistantMessage += "\n```\n";
                        inThink = false;
                    }

                    turn.AssistantMessage += delta;
                    chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
                    yield return CreateChatOutput(chatHistory);
                }
                break;
            case "response.output_item.done":
                var outputItem = TryGetJsonProperty(sseEvent.Data, "item");
                if (ApplyResponseOutputItem(turn, outputItem))
                {
                    chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
                    yield return CreateChatOutput(chatHistory);
                }
                break;
            case "response.completed":
                var response = TryGetJsonProperty(sseEvent.Data, "response");
                if (ApplyResponseCompletedPayload(turn, response))
                {
                    chatHistory[^1].AiMessage.TextMessage = BuildAssistantDisplayText(turn);
                    yield return CreateChatOutput(chatHistory);
                }
                break;
        }
    }
}

static StringContent BuildStreamRequestContent(IEnumerable<WebChatTurn> chatState, string model, int maxTokens, float temperature, float topP, bool useResponsesApi)
{
    return useResponsesApi
        ? BuildResponsesStreamRequestContent(chatState, model, maxTokens, temperature, topP)
        : BuildChatCompletionsStreamRequestContent(chatState, model, maxTokens, temperature, topP);
}

static StringContent BuildChatCompletionsStreamRequestContent(IEnumerable<WebChatTurn> chatState, string model, int maxTokens, float temperature, float topP)
{
    var messages = new List<ChatCompletionMessage>();
    foreach (var turn in chatState)
    {
        if (!string.IsNullOrWhiteSpace(turn.UserMessage) || turn.Attachments.Count > 0)
        {
            messages.Add(new ChatCompletionMessage
            {
                role = "user",
                content = BuildUserContent(turn.UserMessage, turn.Attachments)
            });
        }

        if (!string.IsNullOrWhiteSpace(turn.AssistantMessage))
        {
            messages.Add(new ChatCompletionMessage
            {
                role = "assistant",
                content = turn.AssistantMessage
            });
        }
    }

    var payload = JsonSerializer.Serialize(new ChatCompletionRequest
    {
        stream = true,
        messages = messages.ToArray(),
        model = model,
        max_completion_tokens = maxTokens,
        temperature = temperature,
        top_p = topP
    }, CreateJsonOptions());

    return new StringContent(payload, Encoding.UTF8, "application/json");
}

static StringContent BuildResponsesStreamRequestContent(IEnumerable<WebChatTurn> chatState, string model, int maxTokens, float temperature, float topP)
{
    var input = new List<object>();
    foreach (var turn in chatState)
    {
        if (!string.IsNullOrWhiteSpace(turn.UserMessage) || turn.Attachments.Count > 0)
        {
            input.Add(new
            {
                role = "user",
                content = BuildResponsesUserContent(turn.UserMessage, turn.Attachments)
            });
        }

        if (!string.IsNullOrWhiteSpace(turn.AssistantMessage))
        {
            input.Add(new
            {
                role = "assistant",
                content = turn.AssistantMessage
            });
        }
    }

    var payload = JsonSerializer.Serialize(new ResponseRequest
    {
        stream = true,
        input = input.ToArray(),
        model = model,
        max_output_tokens = maxTokens,
        temperature = temperature,
        top_p = topP
    }, CreateJsonOptions());

    return new StringContent(payload, Encoding.UTF8, "application/json");
}

static object BuildUserContent(string message, IEnumerable<WebChatAttachment> attachments)
{
    var attachmentList = attachments.ToList();
    if (attachmentList.Count == 0)
    {
        return message;
    }

    var parts = new List<ChatCompletionContentPart>();
    if (!string.IsNullOrWhiteSpace(message))
    {
        parts.Add(new ChatCompletionContentPart
        {
            type = "text",
            text = message
        });
    }

    foreach (var attachment in attachmentList)
    {
        parts.Add(new ChatCompletionContentPart
        {
            type = "image_url",
            image_url = new ChatCompletionImageUrl
            {
                url = attachment.OpenAIImageUrl
            }
        });
    }

    return parts.ToArray();
}

static object BuildResponsesUserContent(string message, IEnumerable<WebChatAttachment> attachments)
{
    var attachmentList = attachments.ToList();
    if (attachmentList.Count == 0)
    {
        return message;
    }

    var parts = new List<object>();
    if (!string.IsNullOrWhiteSpace(message))
    {
        parts.Add(new
        {
            type = "input_text",
            text = message
        });
    }

    foreach (var attachment in attachmentList)
    {
        parts.Add(new
        {
            type = "input_image",
            image_url = attachment.OpenAIImageUrl
        });
    }

    return parts.ToArray();
}

static List<WebChatAttachment> BuildAttachments(string imageUrl, string? localImagePath)
{
    var attachments = new List<WebChatAttachment>();

    if (!string.IsNullOrWhiteSpace(imageUrl))
    {
        attachments.Add(CreateUrlAttachment(imageUrl));
    }

    if (!string.IsNullOrWhiteSpace(localImagePath))
    {
        attachments.Add(CreateLocalAttachment(localImagePath));
    }

    return attachments;
}

static WebChatAttachment CreateUrlAttachment(string rawUrl)
{
    var normalizedUrl = rawUrl.Trim();
    if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri))
    {
        throw new Exception("Image URL is invalid.");
    }

    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
    {
        throw new Exception("Only http, https or data image URLs are supported.");
    }

    return new WebChatAttachment
    {
        DisplayName = GetDisplayNameFromUrl(uri),
        Source = normalizedUrl,
        OpenAIImageUrl = normalizedUrl,
        IsLocalFile = false
    };
}

static WebChatAttachment CreateLocalAttachment(string rawPath)
{
    var fullPath = Path.GetFullPath(rawPath);
    if (!System.IO.File.Exists(fullPath))
    {
        throw new Exception("Uploaded image file does not exist.");
    }

    var extension = Path.GetExtension(fullPath).ToLowerInvariant();
    var mimeType = extension switch
    {
        ".png" => "image/png",
        ".jpg" => "image/jpeg",
        ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => throw new Exception("Only png, jpg, jpeg, gif, bmp and webp images are supported.")
    };

    return new WebChatAttachment
    {
        DisplayName = Path.GetFileName(fullPath),
        Source = fullPath,
        OpenAIImageUrl = $"data:{mimeType};base64,{Convert.ToBase64String(System.IO.File.ReadAllBytes(fullPath))}",
        IsLocalFile = true
    };
}

static string BuildHumanDisplayText(string message, IReadOnlyList<WebChatAttachment> attachments)
{
    if (attachments.Count == 0)
    {
        return message;
    }

    var builder = new StringBuilder();
    if (!string.IsNullOrWhiteSpace(message))
    {
        builder.AppendLine(message.TrimEnd());
        builder.AppendLine();
    }

    builder.AppendLine("[Attached Images]");
    foreach (var attachment in attachments)
    {
        builder.Append("- ");
        builder.Append(attachment.IsLocalFile ? "Local image: " : "Image URL: ");
        builder.AppendLine(attachment.Source);
    }

    return builder.ToString().TrimEnd();
}

static string GetDisplayNameFromUrl(Uri uri)
{
    if (string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
    {
        return "inline-image";
    }

    var segment = uri.Segments.LastOrDefault();
    return string.IsNullOrWhiteSpace(segment) ? uri.Host : Uri.UnescapeDataString(segment.Trim('/'));
}

static List<WebChatAttachment> CloneAttachments(IEnumerable<WebChatAttachment> attachments)
{
    return attachments
        .Select(item => new WebChatAttachment
        {
            DisplayName = item.DisplayName,
            Source = item.Source,
            OpenAIImageUrl = item.OpenAIImageUrl,
            IsLocalFile = item.IsLocalFile
        })
        .ToList();
}

static List<WebChatTurn> BuildChatStateFromHistory(IList<ChatbotMessagePair> chatHistory)
{
    if (chatHistory.Count == 0)
    {
        return [];
    }

    return chatHistory
        .Select(CreateTurnFromChatPair)
        .ToList();
}

static WebChatTurn CreateTurnFromChatPair(ChatbotMessagePair pair)
{
    var (userMessage, attachments) = ParseHumanDisplayText(pair.HumanMessage.TextMessage ?? string.Empty);
    var (assistantMessage, additionalProperties) = ParseAssistantDisplayText(pair.AiMessage.TextMessage ?? string.Empty);
    return new WebChatTurn
    {
        UserMessage = userMessage,
        AssistantMessage = assistantMessage,
        AssistantAdditionalProperties = additionalProperties,
        Attachments = attachments
    };
}

static (string Message, List<WebChatAttachment> Attachments) ParseHumanDisplayText(string rawText)
{
    const string marker = "\n[Attached Images]\n";
    if (string.IsNullOrWhiteSpace(rawText))
    {
        return (string.Empty, []);
    }

    var normalizedText = rawText.Replace("\r\n", "\n");
    var markerIndex = normalizedText.IndexOf(marker, StringComparison.Ordinal);
    if (markerIndex < 0)
    {
        return (rawText, []);
    }

    var message = normalizedText[..markerIndex].TrimEnd();
    var attachmentLines = normalizedText[(markerIndex + marker.Length)..]
        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var attachments = new List<WebChatAttachment>();
    foreach (var line in attachmentLines)
    {
        if (!line.StartsWith("- ", StringComparison.Ordinal))
        {
            continue;
        }

        var payload = line[2..];
        if (payload.StartsWith("Image URL: ", StringComparison.Ordinal))
        {
            attachments.Add(CreateUrlAttachment(payload["Image URL: ".Length..]));
            continue;
        }

        if (payload.StartsWith("Local image: ", StringComparison.Ordinal))
        {
            attachments.Add(CreateLocalAttachment(payload["Local image: ".Length..]));
        }
    }

    return (message, attachments);
}

static (string AssistantMessage, string AdditionalProperties) ParseAssistantDisplayText(string rawText)
{
    if (string.IsNullOrWhiteSpace(rawText))
    {
        return (string.Empty, string.Empty);
    }

    var normalizedText = rawText.Replace("\r\n", "\n");
    var markerIndex = normalizedText.IndexOf(AdditionalPropertiesDetailsStart, StringComparison.Ordinal);
    if (markerIndex < 0)
    {
        return (rawText, string.Empty);
    }

    var assistantMessage = normalizedText[..markerIndex].TrimEnd();
    var preStartIndex = normalizedText.IndexOf(AdditionalPropertiesPreStart, markerIndex, StringComparison.Ordinal);
    if (preStartIndex < 0)
    {
        return (assistantMessage, string.Empty);
    }

    preStartIndex += AdditionalPropertiesPreStart.Length;
    var preEndIndex = normalizedText.IndexOf("</pre>", preStartIndex, StringComparison.Ordinal);
    if (preEndIndex < 0)
    {
        return (assistantMessage, string.Empty);
    }

    var encodedAdditionalProperties = normalizedText[preStartIndex..preEndIndex];
    return (assistantMessage, WebUtility.HtmlDecode(encodedAdditionalProperties));
}

static string BuildAssistantDisplayText(WebChatTurn turn)
{
    var builder = new StringBuilder();
    if (!string.IsNullOrWhiteSpace(turn.AssistantMessage))
    {
        builder.Append(turn.AssistantMessage.TrimEnd());
    }

    if (!string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties))
    {
        if (builder.Length > 0)
        {
            builder.AppendLine().AppendLine();
        }

        builder.AppendLine(AdditionalPropertiesDetailsStart);
        builder.AppendLine(AdditionalPropertiesSummary);
        builder.Append(AdditionalPropertiesPreStart);
        builder.Append(WebUtility.HtmlEncode(turn.AssistantAdditionalProperties));
        builder.AppendLine("</pre>");
        builder.Append("</details>");
    }

    return builder.ToString();
}

static string GetChatApiPath(bool useResponsesApi)
{
    return useResponsesApi ? "/responses" : "/chat/completions";
}

static async Task<SseEvent?> ReadSseEventAsync(StreamReader reader)
{
    string? eventName = null;
    var dataBuilder = new StringBuilder();

    while (true)
    {
        var line = await reader.ReadLineAsync();
        Console.WriteLine(line);
        if (line is null)
        {
            break;
        }

        if (string.IsNullOrEmpty(line))
        {
            if (!string.IsNullOrWhiteSpace(eventName) || dataBuilder.Length > 0)
            {
                break;
            }

            continue;
        }

        if (line.StartsWith("event:", StringComparison.Ordinal))
        {
            eventName = line[6..].Trim();
            continue;
        }

        if (line.StartsWith("data:", StringComparison.Ordinal))
        {
            if (dataBuilder.Length > 0)
            {
                dataBuilder.AppendLine();
            }

            dataBuilder.Append(line[5..].Trim());
        }
    }

    if (string.IsNullOrWhiteSpace(eventName) && dataBuilder.Length == 0)
    {
        return null;
    }

    return new SseEvent(eventName, dataBuilder.ToString());
}

static string? TryGetJsonStringProperty(string json, string propertyName)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }
    }
    catch (JsonException)
    {
    }

    return null;
}

static JsonElement? TryGetJsonProperty(string json, string propertyName)
{
    try
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var property))
        {
            return property.Clone();
        }
    }
    catch (JsonException)
    {
    }

    return null;
}

static bool ApplyResponseOutputItem(WebChatTurn turn, JsonElement? itemElement)
{
    if (itemElement is not JsonElement item || item.ValueKind != JsonValueKind.Object)
    {
        return false;
    }

    var changed = false;

    if (TryExtractResponseAdditionalProperties(item, out var additionalProperties)
        && !string.Equals(turn.AssistantAdditionalProperties, additionalProperties, StringComparison.Ordinal))
    {
        turn.AssistantAdditionalProperties = additionalProperties;
        changed = true;
    }

    if (string.IsNullOrWhiteSpace(turn.AssistantMessage))
    {
        var text = ExtractResponseTextFromItem(item);
        if (!string.IsNullOrWhiteSpace(text))
        {
            turn.AssistantMessage = text;
            changed = true;
        }
    }

    return changed;
}

static bool ApplyResponseCompletedPayload(WebChatTurn turn, JsonElement? responseElement)
{
    if (responseElement is not JsonElement response || response.ValueKind != JsonValueKind.Object)
    {
        return false;
    }

    var changed = false;

    if (string.IsNullOrWhiteSpace(turn.AssistantMessage))
    {
        var text = ExtractResponseTextFromResponse(response);
        if (!string.IsNullOrWhiteSpace(text))
        {
            turn.AssistantMessage = text;
            changed = true;
        }
    }

    if (string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties)
        && response.TryGetProperty("output", out var output)
        && output.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in output.EnumerateArray())
        {
            if (TryExtractResponseAdditionalProperties(item, out var additionalProperties))
            {
                turn.AssistantAdditionalProperties = additionalProperties;
                changed = true;
                break;
            }
        }
    }

    return changed;
}

static string ExtractResponseTextFromResponse(JsonElement response)
{
    if (!response.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
    {
        return string.Empty;
    }

    var builder = new StringBuilder();
    foreach (var item in output.EnumerateArray())
    {
        var text = ExtractResponseTextFromItem(item);
        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine().AppendLine();
        }

        builder.Append(text);
    }

    return builder.ToString();
}

static string ExtractResponseTextFromItem(JsonElement item)
{
    if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
    {
        return string.Empty;
    }

    var builder = new StringBuilder();
    foreach (var part in content.EnumerateArray())
    {
        if (!part.TryGetProperty("text", out var textElement) || textElement.ValueKind != JsonValueKind.String)
        {
            continue;
        }

        var text = textElement.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            continue;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine().AppendLine();
        }

        builder.Append(text);
    }

    return builder.ToString();
}

static bool TryExtractResponseAdditionalProperties(JsonElement item, out string formattedAdditionalProperties)
{
    formattedAdditionalProperties = string.Empty;
    if (!item.TryGetProperty("additional_properties", out var additionalProperties)
        || additionalProperties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
    {
        return false;
    }

    formattedAdditionalProperties = JsonSerializer.Serialize(additionalProperties, CreateJsonOptions());
    return !string.IsNullOrWhiteSpace(formattedAdditionalProperties);
}

static string FormatAdditionalProperties(Dictionary<string, object?>? additionalProperties)
{
    if (additionalProperties is null || additionalProperties.Count == 0)
    {
        return string.Empty;
    }

    return JsonSerializer.Serialize(additionalProperties, CreateJsonOptions());
}

static JsonSerializerOptions CreateJsonOptions()
{
    return new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
}

static int ParseMaxTokens(string rawValue)
{
    return int.TryParse(rawValue, out var result) ? result : 4096;
}

static float ParseTemperature(string rawValue)
{
    if (float.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
        || float.TryParse(rawValue, out result))
    {
        if (result is >= 0 and <= 2)
        {
            return result;
        }
    }

    throw new Exception("Temperature must be a valid number between 0 and 2.");
}

static float ParseTopP(string rawValue)
{
    if (float.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result)
        || float.TryParse(rawValue, out result))
    {
        if (result is > 0 and <= 1)
        {
            return result;
        }
    }

    throw new Exception("Top P must be a valid number between 0 and 1.");
}

static Task<Output> SaveSettings(Input input)
{
    ParseMaxTokens(Textbox.Payload(input.Data[3]));
    ParseTemperature(Textbox.Payload(input.Data[4]));
    ParseTopP(Textbox.Payload(input.Data[5]));
    _ = Checkbox.Payload(input.Data[6]);

    return Task.FromResult(gr.Output("Settings saved in this browser."));
}


static string? ParseOptionalLocalImagePath(object? rawValue)
{
    if (IsEmptyFileValue(rawValue))
    {
        return null;
    }

    try
    {
        return Gradio.Net.File.Payload(rawValue!);
    }
    catch (JsonException) when (IsEmptyFileValue(rawValue))
    {
        return null;
    }
}

static bool IsEmptyFileValue(object? rawValue)
{
    if (rawValue is null)
    {
        return true;
    }

    if (rawValue is string text)
    {
        return string.IsNullOrWhiteSpace(text);
    }

    if (rawValue is not JsonElement jsonElement)
    {
        return false;
    }

    return jsonElement.ValueKind switch
    {
        JsonValueKind.Null => true,
        JsonValueKind.Undefined => true,
        JsonValueKind.String => string.IsNullOrWhiteSpace(jsonElement.GetString()),
        JsonValueKind.Array => jsonElement.GetArrayLength() == 0,
        _ => false
    };
}

static class Utils
{
    public static readonly HttpClient Client = new();
    public static ConfigModels Config = new();
}

public record ConfigModels
{
    public int Current { get; set; }
    public List<ConfigModel> Models { get; set; } = [];
}

public record ConfigModel
{
    public string Name { get; set; } = string.Empty;
}

public class WebChatTurn
{
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantMessage { get; set; } = string.Empty;
    public string AssistantAdditionalProperties { get; set; } = string.Empty;
    public List<WebChatAttachment> Attachments { get; set; } = [];
}

public class WebChatAttachment
{
    public string DisplayName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string OpenAIImageUrl { get; set; } = string.Empty;
    public bool IsLocalFile { get; set; }
}

public sealed record SseEvent(string? EventName, string Data);
