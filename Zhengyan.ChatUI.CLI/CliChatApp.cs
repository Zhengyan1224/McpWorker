using System.Globalization;
using System.Text;
using System.Text.Unicode;
using Zhengyan.ChatUI.CLI.Models;
using Zhengyan.ChatUI.CLI.Services;

namespace Zhengyan.ChatUI.CLI;

public sealed class CliChatApp : IDisposable
{
    private static readonly IReadOnlyList<InputSuggestion> SlashCommandSuggestions =
    [
        new("/help", "Show help and command usage"),
        new("/settings", "Show current settings and config path"),
        new("/save", "Save settings to disk"),
        new("/models", "Fetch available models from McpHost"),
        new("/use", "Switch server-side model by index or name"),
        new("/set", "Update local settings"),
        new("/image", "Manage pending image attachments"),
        new("/copy", "Copy the latest assistant output"),
        new("/multiline", "Open the multiline editor"),
        new("/retry", "Retry the latest turn"),
        new("/clear", "Clear chat history and attachments"),
        new("/history", "Show conversation history"),
        new("/exit", "Exit the CLI")
    ];

    private readonly McpHostCliClient _client = new();
    private readonly ConsoleInputEditor _editor = new();
    private readonly List<ChatTurn> _chatHistory = [];
    private readonly List<string> _inputHistory = [];
    private readonly List<ChatImageAttachment> _pendingAttachments = [];
    private CliAppSettings _settings;
    private ConfigModels? _configModels;
    private bool _isDirty;
    private bool _exitRequested;

    public CliChatApp()
    {
        _settings = LoadSettings();
        Console.CancelKeyPress += OnCancelKeyPress;
    }

    public async Task<int> RunAsync(string[] args)
    {
        var launchOptions = CliLaunchOptions.Parse(args);

        if (launchOptions.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        if (launchOptions.ShowConfigPath)
        {
            Console.WriteLine(CliSettingsStore.GetSettingsPath());
            return 0;
        }

        await ApplyStandardInputAsync(launchOptions);

        ApplyLaunchOptions(launchOptions);
        if (launchOptions.SaveSettings)
        {
            SaveIfDirty(true);
        }

        if (launchOptions.IsNonInteractive)
        {
            return await RunSingleShotAsync(launchOptions);
        }

        PrintBanner();
        PrintHint();

        while (!_exitRequested)
        {
            var input = ReadInteractiveInput();
            if (input is null)
            {
                break;
            }

            input = input.TrimEnd();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            try
            {
                RememberInput(input);

                if (input.StartsWith("//", StringComparison.Ordinal))
                {
                    await SendMessageAsync(input[1..]);
                    continue;
                }

                if (input.StartsWith("/", StringComparison.Ordinal))
                {
                    if (!await ExecuteCommandAsync(input))
                    {
                        break;
                    }

                    continue;
                }

                await SendMessageAsync(input);
            }
            catch (Exception ex)
            {
                WriteLineColor(ex.Message, ConsoleColor.Red);
            }
        }

        SaveIfDirty(false);
        return 0;
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
        _client.Dispose();
    }

    private async Task<bool> ExecuteCommandAsync(string input)
    {
        var commandText = input[1..].Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            PrintHint();
            return true;
        }

        var spaceIndex = commandText.IndexOf(' ');
        var command = (spaceIndex < 0 ? commandText : commandText[..spaceIndex]).Trim().ToLowerInvariant();
        var arguments = spaceIndex < 0 ? string.Empty : commandText[(spaceIndex + 1)..].Trim();

        switch (command)
        {
            case "help":
            case "h":
                PrintHelp();
                break;
            case "settings":
                PrintSettings();
                break;
            case "save":
                SaveIfDirty(true);
                break;
            case "models":
                await LoadModelsAsync();
                break;
            case "use":
                await UseModelAsync(arguments);
                break;
            case "set":
                await HandleSetAsync(arguments);
                break;
            case "image":
            case "img":
                HandleImage(arguments);
                break;
            case "copy":
                HandleCopy(arguments);
                break;
            case "retry":
                await RetryAsync();
                break;
            case "clear":
                ClearChat();
                break;
            case "history":
                PrintHistory();
                break;
            case "multiline":
                await SendMultilineAsync();
                break;
            case "exit":
            case "quit":
            case "q":
                _exitRequested = true;
                return false;
            default:
                WriteLineColor($"Unknown command: /{command}. Type /help for usage.", ConsoleColor.Red);
                break;
        }

        return true;
    }

    private async Task HandleSetAsync(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            WriteLineColor("Usage: /set <server|token|model|max_tokens|temperature|top_p|api> <value>", ConsoleColor.Yellow);
            return;
        }

        var spaceIndex = arguments.IndexOf(' ');
        var key = (spaceIndex < 0 ? arguments : arguments[..spaceIndex]).Trim().ToLowerInvariant();
        var value = spaceIndex < 0 ? string.Empty : arguments[(spaceIndex + 1)..].Trim();

        switch (key)
        {
            case "server":
            case "endpoint":
                RequireValue(key, value);
                _settings.ServerEndpoint = value;
                MarkDirty();
                WriteLineColor($"Server endpoint set to: {_settings.ServerEndpoint}", ConsoleColor.Green);
                break;
            case "token":
            case "apikey":
            case "api_key":
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = PromptSecret("API Key");
                }

                _settings.ApiKey = value;
                MarkDirty();
                WriteLineColor("API key updated.", ConsoleColor.Green);
                break;
            case "model":
                RequireValue(key, value);
                _settings.Model = value;
                MarkDirty();
                WriteLineColor($"Model set to: {_settings.Model}", ConsoleColor.Green);
                break;
            case "max_tokens":
            case "maxtokens":
                _settings.MaxTokens = ParseMaxTokens(value);
                MarkDirty();
                WriteLineColor($"Max tokens set to: {_settings.MaxTokens}", ConsoleColor.Green);
                break;
            case "temperature":
                _settings.Temperature = ParseTemperature(value);
                MarkDirty();
                WriteLineColor($"Temperature set to: {_settings.Temperature.ToString("0.###", CultureInfo.InvariantCulture)}", ConsoleColor.Green);
                break;
            case "top_p":
            case "topp":
                _settings.TopP = ParseTopP(value);
                MarkDirty();
                WriteLineColor($"Top P set to: {_settings.TopP.ToString("0.###", CultureInfo.InvariantCulture)}", ConsoleColor.Green);
                break;
            case "api":
            case "apimode":
                RequireValue(key, value);
                _settings.UseResponsesApi = ParseApiMode(value);
                MarkDirty();
                WriteLineColor($"API mode set to: {(_settings.UseResponsesApi ? "responses" : "chat")}", ConsoleColor.Green);
                break;
            default:
                WriteLineColor($"Unsupported setting: {key}", ConsoleColor.Red);
                break;
        }

        await Task.CompletedTask;
    }

    private void HandleImage(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            PrintPendingAttachments();
            return;
        }

        var spaceIndex = arguments.IndexOf(' ');
        var action = (spaceIndex < 0 ? arguments : arguments[..spaceIndex]).Trim().ToLowerInvariant();
        var value = spaceIndex < 0 ? string.Empty : arguments[(spaceIndex + 1)..].Trim();

        switch (action)
        {
            case "add-url":
                RequireValue(action, value);
                _pendingAttachments.Add(_client.CreateUrlAttachment(value));
                WriteLineColor("Image URL attached to the next message.", ConsoleColor.Green);
                break;
            case "add-file":
            case "add-path":
                RequireValue(action, value);
                _pendingAttachments.Add(_client.CreateLocalAttachment(value));
                WriteLineColor("Local image attached to the next message.", ConsoleColor.Green);
                break;
            case "list":
                PrintPendingAttachments();
                break;
            case "clear":
                _pendingAttachments.Clear();
                WriteLineColor("Pending image attachments cleared.", ConsoleColor.Green);
                break;
            case "remove":
                var index = ParseAttachmentIndex(value);
                var removed = _pendingAttachments[index];
                _pendingAttachments.RemoveAt(index);
                WriteLineColor($"Removed attachment: {removed.DisplayName}", ConsoleColor.Green);
                break;
            default:
                WriteLineColor("Usage: /image <add-url|add-file|list|remove|clear> ...", ConsoleColor.Yellow);
                break;
        }
    }

    private async Task LoadModelsAsync()
    {
        var models = await _client.GetModelsAsync(_settings.ServerEndpoint, _settings.ApiKey);
        _configModels = models;

        if (models.Current >= 0 && models.Current < models.Models.Count && string.IsNullOrWhiteSpace(_settings.Model))
        {
            _settings.Model = models.Models[models.Current].Name;
            MarkDirty();
        }

        WriteLineColor("Available models:", ConsoleColor.Cyan);
        for (var index = 0; index < models.Models.Count; index++)
        {
            var marker = index == models.Current ? "*" : " ";
            Console.WriteLine($"  {marker} {index + 1,2}. {models.Models[index].Name}");
        }
    }

    private async Task UseModelAsync(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            WriteLineColor("Usage: /use <model-index|model-name>", ConsoleColor.Yellow);
            return;
        }

        if (_configModels?.Models.Count is not > 0)
        {
            await LoadModelsAsync();
        }

        if (_configModels?.Models.Count is not > 0)
        {
            throw new InvalidOperationException("No models available.");
        }

        var modelIndex = ResolveModelIndex(arguments, _configModels);
        await _client.SwitchModelAsync(_settings.ServerEndpoint, _settings.ApiKey, modelIndex);
        _configModels.Current = modelIndex;
        _settings.Model = _configModels.Models[modelIndex].Name;
        MarkDirty();
        WriteLineColor($"Switched to model: {_settings.Model}", ConsoleColor.Green);
    }

    private async Task RetryAsync()
    {
        if (_chatHistory.Count == 0)
        {
            WriteLineColor("No chat history available for regeneration.", ConsoleColor.Red);
            return;
        }

        var lastTurn = _chatHistory[^1];
        var userMessage = lastTurn.UserMessage;
        var attachments = lastTurn.Attachments.Select(static item => item.Clone()).ToList();
        _chatHistory.RemoveAt(_chatHistory.Count - 1);

        await SendMessageAsync(userMessage, attachments);
    }

    private void ClearChat()
    {
        _chatHistory.Clear();
        _pendingAttachments.Clear();
        WriteLineColor("Chat history and pending attachments cleared.", ConsoleColor.Green);
    }

    private void PrintHistory()
    {
        if (_chatHistory.Count == 0)
        {
            WriteLineColor("No chat history yet.", ConsoleColor.DarkYellow);
            return;
        }

        WriteLineColor("Conversation history:", ConsoleColor.Cyan);
        for (var index = 0; index < _chatHistory.Count; index++)
        {
            var turn = _chatHistory[index];
            var preview = turn.UserMessage.Replace(Environment.NewLine, " ").Trim();
            if (preview.Length > 72)
            {
                preview = preview[..69] + "...";
            }

            Console.WriteLine($"  {index + 1,2}. {preview}");
        }
    }

    private async Task SendMultilineAsync()
    {
        if (!_editor.CanUseInteractiveConsole)
        {
            await SendMultilineFallbackAsync();
            return;
        }

        if (_pendingAttachments.Count > 0)
        {
            WriteLineColor($"Pending attachments: {_pendingAttachments.Count}", ConsoleColor.DarkCyan);
        }

        var title = "Ctrl+D submit | Esc cancel | Ctrl+L clear | Enter newline";
        var hint = _pendingAttachments.Count > 0
            ? $"Attachments {_pendingAttachments.Count} | Arrows move | Home/End jump | Tab inserts spaces"
            : "Arrows move | Home/End jump | Tab inserts spaces";
        var message = _editor.ReadMultiline(title, hint);
        if (message is null)
        {
            WriteLineColor("Multiline input cancelled.", ConsoleColor.DarkYellow);
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            WriteLineColor("No message entered.", ConsoleColor.DarkYellow);
            return;
        }

        RememberInput(message);
        await SendMessageAsync(message);
    }

    private async Task<int> RunSingleShotAsync(CliLaunchOptions launchOptions)
    {
        var attachments = BuildLaunchAttachments(launchOptions);
        if (string.IsNullOrWhiteSpace(launchOptions.Message) && attachments.Count == 0)
        {
            WriteLineColor("Nothing to send.", ConsoleColor.DarkYellow);
            return 1;
        }

        var success = await SendMessageAsync(launchOptions.Message ?? string.Empty, attachments);
        if (success && !string.IsNullOrWhiteSpace(launchOptions.OutputPath))
        {
            SaveOutputToFile(launchOptions.OutputPath);
        }

        SaveIfDirty(false);
        return success ? 0 : 1;
    }

    private async Task<bool> SendMessageAsync(string message)
    {
        var success = await SendMessageAsync(message, _pendingAttachments.Select(static item => item.Clone()).ToList());
        _pendingAttachments.Clear();
        return success;
    }

    private async Task<bool> SendMessageAsync(string message, List<ChatImageAttachment> attachments)
    {
        if (string.IsNullOrWhiteSpace(_settings.Model))
        {
            WriteLineColor("No model selected. Use /models, /use or /set model <name> first.", ConsoleColor.Red);
            return false;
        }

        if (string.IsNullOrWhiteSpace(message) && attachments.Count == 0)
        {
            WriteLineColor("Nothing to send.", ConsoleColor.DarkYellow);
            return false;
        }

        var turn = new ChatTurn
        {
            UserMessage = message
        };

        foreach (var attachment in attachments)
        {
            turn.Attachments.Add(attachment.Clone());
        }

        _chatHistory.Add(turn);
        WriteUserTurn(turn);

        var lastReasoningLength = 0;
        var lastAssistantLength = 0;
        var printedThinkingHeader = false;
        var printedAssistantHeader = false;
        var reasoningColumn = 0;
        var assistantColumn = 0;

        void Refresh()
        {
            if (turn.AssistantReasoning.Length > lastReasoningLength)
            {
                if (!printedThinkingHeader)
                {
                    Console.WriteLine();
                    WriteSectionHeader("Thinking", ConsoleColor.DarkYellow);
                    printedThinkingHeader = true;
                    reasoningColumn = 0;
                }

                WriteStreamingColor(turn.AssistantReasoning[lastReasoningLength..], ConsoleColor.DarkYellow, ref reasoningColumn);
                lastReasoningLength = turn.AssistantReasoning.Length;
            }

            if (turn.AssistantMessage.Length > lastAssistantLength)
            {
                if (!printedAssistantHeader)
                {
                    Console.WriteLine();
                    WriteSectionHeader("Assistant", ConsoleColor.Cyan);
                    printedAssistantHeader = true;
                    assistantColumn = 0;
                }

                WriteStreamingColor(turn.AssistantMessage[lastAssistantLength..], ConsoleColor.Gray, ref assistantColumn);
                lastAssistantLength = turn.AssistantMessage.Length;
            }
        }

        try
        {
            await _client.StreamChatAsync(
                _settings.ServerEndpoint,
                _settings.ApiKey,
                _settings.Model,
                _settings.UseResponsesApi,
                _settings.MaxTokens,
                _settings.Temperature,
                _settings.TopP,
                _chatHistory,
                turn,
                Refresh);

            Refresh();
            Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties))
            {
                WriteSectionHeader("Additional", ConsoleColor.Magenta);
                Console.WriteLine(turn.AssistantAdditionalProperties);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            WriteLineColor($"Request failed: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    private void HandleCopy(string arguments)
    {
        var turn = GetLastTurnWithOutput();
        if (turn is null)
        {
            WriteLineColor("No assistant output is available to copy.", ConsoleColor.Red);
            return;
        }

        var part = string.IsNullOrWhiteSpace(arguments) ? "assistant" : arguments.Trim().ToLowerInvariant();
        var text = BuildCopyText(turn, part);
        if (string.IsNullOrWhiteSpace(text))
        {
            WriteLineColor($"No content available for copy target: {part}", ConsoleColor.Red);
            return;
        }

        if (!ClipboardService.TrySetText(text, out var error))
        {
            WriteLineColor(error ?? "Clipboard copy failed.", ConsoleColor.Red);
            return;
        }

        WriteLineColor($"Copied {part} to clipboard.", ConsoleColor.Green);
    }

    private void SaveOutputToFile(string outputPath)
    {
        var turn = GetLastTurnWithOutput();
        if (turn is null)
        {
            WriteLineColor("No assistant output is available to write.", ConsoleColor.Red);
            return;
        }

        var content = BuildRenderedOutput(turn);
        if (string.IsNullOrWhiteSpace(content))
        {
            WriteLineColor("No assistant output is available to write.", ConsoleColor.Red);
            return;
        }

        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(false));
        WriteLineColor($"Response written to: {fullPath}", ConsoleColor.Green);
    }

    private void PrintSettings()
    {
        WriteLineColor("Current settings:", ConsoleColor.Cyan);
        Console.WriteLine($"  ServerEndpoint : {_settings.ServerEndpoint}");
        Console.WriteLine($"  ApiKey         : {MaskSecret(_settings.ApiKey)}");
        Console.WriteLine($"  Model          : {_settings.Model}");
        Console.WriteLine($"  MaxTokens      : {_settings.MaxTokens}");
        Console.WriteLine($"  Temperature    : {_settings.Temperature.ToString("0.###", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"  TopP           : {_settings.TopP.ToString("0.###", CultureInfo.InvariantCulture)}");
        Console.WriteLine($"  ApiMode        : {(_settings.UseResponsesApi ? "responses" : "chat")}");
        Console.WriteLine($"  ConfigPath     : {CliSettingsStore.GetSettingsPath()}");
    }

    private void PrintPendingAttachments()
    {
        if (_pendingAttachments.Count == 0)
        {
            WriteLineColor("No pending image attachments.", ConsoleColor.DarkYellow);
            return;
        }

        WriteLineColor("Pending image attachments:", ConsoleColor.Cyan);
        for (var index = 0; index < _pendingAttachments.Count; index++)
        {
            var item = _pendingAttachments[index];
            Console.WriteLine($"  {index + 1,2}. {item.SourceLabel}: {item.Source}");
        }
    }

    private void PrintBanner()
    {
        WriteLineColor("Zhengyan.ChatUI.CLI", ConsoleColor.Cyan);
        Console.WriteLine("Interactive CLI client for McpHost chat testing.");
        Console.WriteLine();
    }

    private void PrintHint()
    {
        WriteWrappedLine("Type a message and press Enter to send it. Use /help to see all commands.");
        WriteWrappedLine("Editor keys: Up/Down history or slash-menu select | Left/Right move | Tab accept slash command | Ctrl+A/E/U/K | Ctrl+L clear | /multiline for full editor.");
        WriteWrappedLine("Use /copy to copy the latest assistant output. Pipe stdin to send once, or use --stdin to append piped text.");
        WriteWrappedLine("Use //text to send a message that starts with '/'.");
        Console.WriteLine();
    }

    private static void PrintHelp()
    {
        PrintHelpSection("Core commands",
        [
            ("/help", "Show this help text"),
            ("/settings", "Show current settings and config path"),
            ("/save", "Save settings to disk"),
            ("/models", "Fetch available models from McpHost"),
            ("/use <index|name>", "Switch server-side model from fetched models"),
            ("/set <key> <value>", "Update local settings"),
            ("/image ...", "Manage pending image attachments"),
            ("/copy [assistant|thinking|additional|all]", "Copy the latest assistant output to the clipboard"),
            ("/multiline", "Open the realtime draft editor"),
            ("/retry", "Retry the latest turn"),
            ("/clear", "Clear chat history and pending images"),
            ("/history", "Show conversation history"),
            ("/exit", "Exit the CLI"),
            ("Ctrl+C", "Exit after the current request finishes")
        ]);

        PrintHelpSection("Settings keys",
        [
            ("server", "McpHost API endpoint"),
            ("token", "API key (omit value to enter it securely)"),
            ("model", "Current model name"),
            ("max_tokens", "Max completion/output tokens"),
            ("temperature", "Sampling temperature, range 0-2"),
            ("top_p", "Sampling top_p, range 0-1"),
            ("api", "chat or responses")
        ]);

        PrintHelpSection("Inline editor",
        [
            ("Enter", "Submit the current line"),
            ("/", "Open the slash command menu"),
            ("Up/Down", "Browse local input history"),
            ("Left/Right", "Move inside the current line"),
            ("Tab", "Accept the highlighted slash command"),
            ("Home/End", "Jump to the start/end of the line"),
            ("Ctrl+A / Ctrl+E", "Jump to the start/end of the line"),
            ("Ctrl+U / Ctrl+K", "Delete before/after the cursor"),
            ("Ctrl+L", "Clear the current line"),
            ("Ctrl+D", "Exit when the line is empty"),
            ("Esc", "Clear the current line")
        ]);

        PrintHelpSection("Image commands",
        [
            ("/image add-url <url>", "Attach an image URL to the next message"),
            ("/image add-file <path>", "Attach a local image file to the next message"),
            ("/image list", "Show pending image attachments"),
            ("/image remove <index>", "Remove one pending image"),
            ("/image clear", "Clear pending image attachments")
        ]);

        PrintHelpSection("Multiline editor",
        [
            ("/multiline", "Open the realtime draft editor"),
            ("Enter", "Insert a new line"),
            ("Ctrl+D", "Submit the current draft"),
            ("Ctrl+A / Ctrl+E", "Jump to the start/end of the current line"),
            ("Ctrl+U / Ctrl+K", "Delete before/after the cursor on the current line"),
            ("Ctrl+L", "Clear the whole draft"),
            ("Esc", "Abort the draft"),
            ("Arrows/Home/End", "Move the cursor inside the draft"),
            ("Tab", "Insert spaces")
        ]);

        PrintHelpSection("Non-interactive mode",
        [
            ("--message, -m <text>", "Send one message and exit"),
            ("<text>", "Positional prompt, sent once and then exits"),
            ("--stdin", "Read piped stdin and use/append it as message text"),
            ("--output <path>", "Write the rendered response to a file after success"),
            ("--file <path>", "Attach a local image file, repeatable"),
            ("--image-url <url>", "Attach an image URL, repeatable"),
            ("--server <url>", "Override server endpoint"),
            ("--token <key>", "Override API key"),
            ("--model <name>", "Override model"),
            ("--max-tokens <value>", "Override max_tokens"),
            ("--temperature <value>", "Override temperature"),
            ("--top-p <value>", "Override top_p"),
            ("--api <chat|responses>", "Override API mode"),
            ("--save", "Persist CLI overrides to the local config")
        ]);

        PrintExamples("Examples",
        [
            "/copy assistant",
            "/copy all",
            "/set server http://localhost:9083/mcphost/api/v1",
            "/set token",
            "/models",
            "/use 1",
            "/set temperature 0.7",
            "/set api responses",
            "/image add-file D:\\demo\\chart.png",
            "Explain transformer KV cache in simple language.",
            "Get-Content prompt.txt | dotnet run --project Zhengyan.ChatUI.CLI\\Zhengyan.ChatUI.CLI.csproj -- --model qwen3.6-plus",
            "dotnet run --project Zhengyan.ChatUI.CLI\\Zhengyan.ChatUI.CLI.csproj -- -m \"Explain MCP in plain language.\"",
            "Get-Content notes.md | dotnet run --project Zhengyan.ChatUI.CLI\\Zhengyan.ChatUI.CLI.csproj -- --stdin -m \"Summarize the following notes:\" --output result.txt",
            "dotnet run --project Zhengyan.ChatUI.CLI\\Zhengyan.ChatUI.CLI.csproj -- \"Summarize MCP in five bullets.\"",
            "dotnet run --project Zhengyan.ChatUI.CLI\\Zhengyan.ChatUI.CLI.csproj -- -m \"Describe this image\" --file D:\\demo\\chart.png --api responses --output answer.md"
        ]);
    }

    private string? ReadInteractiveInput()
    {
        var model = string.IsNullOrWhiteSpace(_settings.Model) ? "no-model" : _settings.Model;
        return _editor.ReadLine($"[{model}] > ", _inputHistory, SlashCommandSuggestions);
    }

    private static void WriteUserTurn(ChatTurn turn)
    {
        WriteSectionHeader("User", ConsoleColor.Green);
        Console.WriteLine(turn.UserMessage);
        if (turn.Attachments.Count > 0)
        {
            foreach (var attachment in turn.Attachments)
            {
                Console.WriteLine($"  - {attachment.SourceLabel}: {attachment.Source}");
            }
        }
    }

    private void SaveIfDirty(bool verbose)
    {
        if (!_isDirty && !verbose)
        {
            return;
        }

        var path = CliSettingsStore.Save(_settings);
        _isDirty = false;
        if (verbose)
        {
            WriteLineColor($"Settings saved: {path}", ConsoleColor.Green);
        }
    }

    private static CliAppSettings LoadSettings()
    {
        try
        {
            return CliSettingsStore.Load();
        }
        catch
        {
            return new CliAppSettings();
        }
    }

    private void MarkDirty()
    {
        _isDirty = true;
    }

    private async Task ApplyStandardInputAsync(CliLaunchOptions launchOptions)
    {
        var hasRedirectedInput = Console.IsInputRedirected;
        var shouldAutoRead = hasRedirectedInput
            && string.IsNullOrWhiteSpace(launchOptions.Message)
            && !launchOptions.ShowHelp
            && !launchOptions.ShowConfigPath;

        if (!launchOptions.ReadMessageFromStdIn && !shouldAutoRead)
        {
            return;
        }

        var standardInput = await Console.In.ReadToEndAsync();
        launchOptions.ApplyStandardInputMessage(standardInput, launchOptions.ReadMessageFromStdIn);
    }

    private void ApplyLaunchOptions(CliLaunchOptions launchOptions)
    {
        var markDirty = launchOptions.SaveSettings;

        if (!string.IsNullOrWhiteSpace(launchOptions.ServerEndpoint))
        {
            _settings.ServerEndpoint = launchOptions.ServerEndpoint;
            MarkDirtyIfRequested(markDirty);
        }

        if (launchOptions.ApiKey is not null)
        {
            _settings.ApiKey = launchOptions.ApiKey;
            MarkDirtyIfRequested(markDirty);
        }

        if (!string.IsNullOrWhiteSpace(launchOptions.Model))
        {
            _settings.Model = launchOptions.Model;
            MarkDirtyIfRequested(markDirty);
        }

        if (launchOptions.MaxTokens is int maxTokens)
        {
            _settings.MaxTokens = ValidateMaxTokens(maxTokens);
            MarkDirtyIfRequested(markDirty);
        }

        if (launchOptions.Temperature is float temperature)
        {
            _settings.Temperature = ValidateTemperature(temperature);
            MarkDirtyIfRequested(markDirty);
        }

        if (launchOptions.TopP is float topP)
        {
            _settings.TopP = ValidateTopP(topP);
            MarkDirtyIfRequested(markDirty);
        }

        if (launchOptions.UseResponsesApi is bool useResponsesApi)
        {
            _settings.UseResponsesApi = useResponsesApi;
            MarkDirtyIfRequested(markDirty);
        }
    }

    private List<ChatImageAttachment> BuildLaunchAttachments(CliLaunchOptions launchOptions)
    {
        var attachments = new List<ChatImageAttachment>();

        foreach (var imageFile in launchOptions.ImageFiles)
        {
            attachments.Add(_client.CreateLocalAttachment(imageFile));
        }

        foreach (var imageUrl in launchOptions.ImageUrls)
        {
            attachments.Add(_client.CreateUrlAttachment(imageUrl));
        }

        return attachments;
    }

    private void MarkDirtyIfRequested(bool shouldMarkDirty)
    {
        if (shouldMarkDirty)
        {
            MarkDirty();
        }
    }

    private void RememberInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (_inputHistory.Count == 0 || !string.Equals(_inputHistory[^1], input, StringComparison.Ordinal))
        {
            _inputHistory.Add(input);
        }
    }

    private async Task SendMultilineFallbackAsync()
    {
        WriteLineColor("Fallback multiline mode: finish with a single '.' line.", ConsoleColor.DarkYellow);
        var builder = new StringBuilder();
        while (true)
        {
            Console.Write("... ");
            var line = Console.ReadLine();
            if (line is null)
            {
                WriteLineColor("Multiline input cancelled.", ConsoleColor.DarkYellow);
                return;
            }

            if (line == ".")
            {
                break;
            }

            builder.AppendLine(line);
        }

        var message = builder.ToString().TrimEnd();
        if (string.IsNullOrWhiteSpace(message))
        {
            WriteLineColor("No message entered.", ConsoleColor.DarkYellow);
            return;
        }

        RememberInput(message);
        await SendMessageAsync(message);
    }

    private ChatTurn? GetLastTurnWithOutput()
    {
        for (var index = _chatHistory.Count - 1; index >= 0; index--)
        {
            var turn = _chatHistory[index];
            if (!string.IsNullOrWhiteSpace(turn.AssistantMessage)
                || !string.IsNullOrWhiteSpace(turn.AssistantReasoning)
                || !string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties))
            {
                return turn;
            }
        }

        return null;
    }

    private static string BuildCopyText(ChatTurn turn, string part)
    {
        return part switch
        {
            "assistant" or "" => turn.AssistantMessage.TrimEnd(),
            "thinking" or "reasoning" => turn.AssistantReasoning.TrimEnd(),
            "additional" => turn.AssistantAdditionalProperties.TrimEnd(),
            "all" => BuildRenderedOutput(turn),
            _ => throw new InvalidOperationException("copy target must be assistant, thinking, additional or all.")
        };
    }

    private static string BuildRenderedOutput(ChatTurn turn)
    {
        var sections = new List<(string Title, string Content)>();
        if (!string.IsNullOrWhiteSpace(turn.AssistantReasoning))
        {
            sections.Add(("Thinking", turn.AssistantReasoning.TrimEnd()));
        }

        if (!string.IsNullOrWhiteSpace(turn.AssistantMessage))
        {
            sections.Add(("Assistant", turn.AssistantMessage.TrimEnd()));
        }

        if (!string.IsNullOrWhiteSpace(turn.AssistantAdditionalProperties))
        {
            sections.Add(("Additional", turn.AssistantAdditionalProperties.TrimEnd()));
        }

        if (sections.Count == 0)
        {
            return string.Empty;
        }

        if (sections.Count == 1 && sections[0].Title == "Assistant")
        {
            return sections[0].Content;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < sections.Count; index++)
        {
            if (index > 0)
            {
                builder.AppendLine().AppendLine();
            }

            builder.Append('[').Append(sections[index].Title).AppendLine("]");
            builder.Append(sections[index].Content);
        }

        return builder.ToString();
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _exitRequested = true;
        Console.WriteLine();
        WriteLineColor("Exit requested. Finishing current operation and saving settings...", ConsoleColor.DarkYellow);
    }

    private static int ParseMaxTokens(string rawValue)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || result <= 0)
        {
            throw new InvalidOperationException("max_tokens must be a positive integer.");
        }

        return ValidateMaxTokens(result);
    }

    private static float ParseTemperature(string rawValue)
    {
        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException("temperature must be a valid number.");
        }

        return ValidateTemperature(result);
    }

    private static float ParseTopP(string rawValue)
    {
        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidOperationException("top_p must be a valid number.");
        }

        return ValidateTopP(result);
    }

    private static bool ParseApiMode(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "responses" or "response" => true,
            "chat" or "completions" => false,
            _ => throw new InvalidOperationException("api must be either 'chat' or 'responses'.")
        };
    }

    private static int ResolveModelIndex(string value, ConfigModels configModels)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oneBasedIndex))
        {
            var zeroBasedIndex = oneBasedIndex - 1;
            if (zeroBasedIndex < 0 || zeroBasedIndex >= configModels.Models.Count)
            {
                throw new InvalidOperationException("Model index is out of range.");
            }

            return zeroBasedIndex;
        }

        var index = configModels.Models.FindIndex(model => string.Equals(model.Name, value, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("Model not found in the fetched model list.");
        }

        return index;
    }

    private int ParseAttachmentIndex(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oneBasedIndex))
        {
            throw new InvalidOperationException("Attachment index must be a number.");
        }

        var zeroBasedIndex = oneBasedIndex - 1;
        if (zeroBasedIndex < 0 || zeroBasedIndex >= _pendingAttachments.Count)
        {
            throw new InvalidOperationException("Attachment index is out of range.");
        }

        return zeroBasedIndex;
    }

    private static void RequireValue(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} requires a value.");
        }
    }

    private static string PromptSecret(string label)
    {
        Console.Write($"{label}: ");
        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        if (value.Length <= 8)
        {
            return new string('*', value.Length);
        }

        return $"{value[..4]}...{value[^4..]}";
    }

    private static int ValidateMaxTokens(int value)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException("max_tokens must be a positive integer.");
        }

        return value;
    }

    private static float ValidateTemperature(float value)
    {
        if (value is < 0 or > 2)
        {
            throw new InvalidOperationException("temperature must be between 0 and 2.");
        }

        return value;
    }

    private static float ValidateTopP(float value)
    {
        if (value is <= 0 or > 1)
        {
            throw new InvalidOperationException("top_p must be between 0 and 1.");
        }

        return value;
    }

    private static void WriteSectionHeader(string title, ConsoleColor color)
    {
        WriteColor($"[{title}]", color);
        Console.WriteLine();
    }

    private static void WriteLineColor(string text, ConsoleColor color)
    {
        WriteColor(text, color);
        Console.WriteLine();
    }

    private static void WriteColor(string text, ConsoleColor color)
    {
        var current = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = current;
    }

    private static void WriteStreamingColor(string text, ConsoleColor color, ref int displayColumn)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var current = Console.ForegroundColor;
        Console.ForegroundColor = color;

        try
        {
            for (var index = 0; index < text.Length;)
            {
                if (text[index] == '\r')
                {
                    index++;
                    continue;
                }

                if (text[index] == '\n')
                {
                    Console.WriteLine();
                    displayColumn = 0;
                    index++;
                    continue;
                }

                var rune = Rune.GetRuneAt(text, index);
                var runeWidth = GetRuneWidth(rune);
                var lineWidth = GetConsoleWidth();
                if (displayColumn > 0 && displayColumn + runeWidth > lineWidth)
                {
                    Console.WriteLine();
                    displayColumn = 0;
                }

                Console.Write(rune.ToString());
                displayColumn += runeWidth;
                index += rune.Utf16SequenceLength;

                if (displayColumn >= lineWidth)
                {
                    Console.WriteLine();
                    displayColumn = 0;
                }
            }
        }
        finally
        {
            Console.ForegroundColor = current;
        }
    }

    private static void PrintHelpSection(string title, IEnumerable<(string Command, string Description)> items)
    {
        Console.WriteLine(title);
        foreach (var (command, description) in items)
        {
            PrintHelpEntry(command, description);
        }

        Console.WriteLine();
    }

    private static void PrintExamples(string title, IEnumerable<string> examples)
    {
        Console.WriteLine(title);
        foreach (var example in examples)
        {
            WriteWrappedLine(example, 2, 2);
        }

        Console.WriteLine();
    }

    private static void PrintHelpEntry(string command, string description)
    {
        var width = GetConsoleWidth();
        var leftWidth = Math.Clamp(width / 3, 18, 28);
        var leftText = "  " + command;
        if (leftText.Length >= leftWidth)
        {
            WriteWrappedLine(leftText, 2, 2);
            WriteWrappedLine(description, leftWidth, leftWidth);
            return;
        }

        var firstLinePrefix = leftText.PadRight(leftWidth);
        var wrapped = WrapText(description, Math.Max(20, width - leftWidth));
        if (wrapped.Count == 0)
        {
            Console.WriteLine(firstLinePrefix.TrimEnd());
            return;
        }

        Console.Write(firstLinePrefix);
        Console.WriteLine(wrapped[0]);
        var continuationPrefix = new string(' ', leftWidth);
        for (var index = 1; index < wrapped.Count; index++)
        {
            Console.Write(continuationPrefix);
            Console.WriteLine(wrapped[index]);
        }
    }

    private static void WriteWrappedLine(string text, int firstIndent = 0, int continuationIndent = 0)
    {
        var width = GetConsoleWidth();
        var availableFirst = Math.Max(20, width - firstIndent);
        var availableContinuation = Math.Max(20, width - continuationIndent);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            Console.WriteLine();
            return;
        }

        var currentIndent = firstIndent;
        var currentWidth = availableFirst;
        var line = new StringBuilder();
        foreach (var word in words)
        {
            var proposedLength = line.Length == 0 ? word.Length : line.Length + 1 + word.Length;
            if (proposedLength > currentWidth && line.Length > 0)
            {
                Console.Write(new string(' ', currentIndent));
                Console.WriteLine(line.ToString());
                line.Clear();
                currentIndent = continuationIndent;
                currentWidth = availableContinuation;
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            Console.Write(new string(' ', currentIndent));
            Console.WriteLine(line.ToString());
        }
    }

    private static List<string> WrapText(string text, int width)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return lines;
        }

        var line = new StringBuilder();
        foreach (var word in words)
        {
            var proposedLength = line.Length == 0 ? word.Length : line.Length + 1 + word.Length;
            if (proposedLength > width && line.Length > 0)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Math.Max(60, Console.WindowWidth - 1);
        }
        catch
        {
            return 100;
        }
    }

    private static int GetRuneWidth(Rune rune)
    {
        var value = rune.Value;
        if (value is >= 0x1100 and <= 0x115F
            or 0x2329 or 0x232A
            or >= 0x2E80 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE10 and <= 0xFE19
            or >= 0xFE30 and <= 0xFE6F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6)
        {
            return 2;
        }

        return Rune.IsControl(rune) ? 0 : 1;
    }
}
