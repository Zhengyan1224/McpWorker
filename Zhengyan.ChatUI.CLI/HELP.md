# Zhengyan.ChatUI.CLI

`Zhengyan.ChatUI.CLI` is a console-based chat test client for `McpHost`.
It mirrors the core capabilities of `Zhengyan.ChatUI.Desktop`, `Zhengyan.ChatUI.TUI` and `Zhengyan.ChatUI.Web`, but uses an interactive CLI workflow.

## Start

Run from the repository root:

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj
```

Show built-in help:

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --help
```

Print the config file path:

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --config-path
```

Run one prompt directly and exit:

```powershell
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --message "Explain MCP in plain language."
```

Pipe stdin into one request:

```powershell
Get-Content prompt.txt | dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --model qwen3.6-plus
```

Pipe stdin and save the rendered result:

```powershell
Get-Content notes.md | dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --stdin -m "Summarize the following notes:" --output result.txt
```

## Basic workflow

1. Start the CLI.
2. Configure endpoint / API key / model with `/set ...`.
3. Fetch available models with `/models`.
4. Switch server-side model with `/use <index|name>`.
5. Type normal text and press Enter to send a message.
6. Use `/image ...` to attach images to the next message.
7. Use the inline editor keys for history/navigation, or `/multiline` for a larger draft editor.
8. Use `/copy` to copy the latest assistant output to the clipboard.
9. Use `/save` to persist settings.

If a message must start with `/`, type `//` at the beginning.

## Commands

### Core

```text
/help
/settings
/save
/models
/use <index|name>
/set <server|token|model|max_tokens|temperature|top_p|api> <value>
/image ...
/copy [assistant|thinking|additional|all]
/multiline
/retry
/clear
/history
/exit
```

### Multiline editor

`/multiline` opens a realtime draft editor.

```text
Enter
Ctrl+D
Ctrl+A / Ctrl+E
Ctrl+U / Ctrl+K
Esc
Ctrl+L
Arrows / Home / End
Tab
```

Behavior:

- `Enter` inserts a new line
- `Ctrl+D` submits the draft
- `Ctrl+A / Ctrl+E` jump to line start/end
- `Ctrl+U / Ctrl+K` delete before/after the cursor on the current line
- `Esc` aborts multiline input
- `Ctrl+L` clears the draft
- `Arrows / Home / End` move the cursor
- `Tab` inserts spaces

### Inline editor

The normal prompt also uses a realtime editor:

```text
Enter
Up / Down
Left / Right
Home / End
Ctrl+A / Ctrl+E
Ctrl+U / Ctrl+K
Ctrl+L
Ctrl+D
Esc
```

Behavior:

- `Enter` submits the current line
- `Up / Down` browse local input history
- `Left / Right` move inside the current line
- `Home / End` jump to line start/end
- `Ctrl+A / Ctrl+E` jump to line start/end
- `Ctrl+U / Ctrl+K` delete before/after the cursor
- `Ctrl+L` clears the current line
- `Ctrl+D` exits when the line is empty
- `Esc` clears the current line

### Images

```text
/image add-url <url>
/image add-file <path>
/image list
/image remove <index>
/image clear
```

### Copy

```text
/copy
/copy assistant
/copy thinking
/copy additional
/copy all
```

Rules:

- `/copy` defaults to the latest assistant message.
- `/copy thinking` copies the latest reasoning block.
- `/copy additional` copies the latest additional payload.
- `/copy all` copies the rendered `Thinking` / `Assistant` / `Additional` sections.

### Non-interactive mode

```text
--message, -m <text>
<text>
--stdin
--output <path>
--file <path>
--image-url <url>
--server <url>
--token <key>
--model <name>
--max-tokens <value>
--temperature <value>
--top-p <value>
--api <chat|responses>
--save
```

Rules:

- When `--message` or a positional prompt is provided, the CLI sends one request and exits.
- When stdin is redirected and no explicit message is provided, the CLI reads stdin as the message text.
- `--stdin` forces the CLI to read stdin and append it after `--message`.
- `--output <path>` writes the rendered response to a UTF-8 text file after a successful request.
- `--file` and `--image-url` are repeatable.
- `--save` persists CLI overrides to the local settings file.
- Without `--save`, overrides only affect the current process.

## Settings

CLI settings are stored in:

`%LocalAppData%\Zhengyan.ChatUI.CLI\settings.json`

Stored fields:

- `ServerEndpoint`
- `ApiKey`
- `Model`
- `MaxTokens`
- `Temperature`
- `TopP`
- `UseResponsesApi`

## Streaming behavior

The CLI shows streamed output in separate blocks:

- `User`
- `Thinking`
- `Assistant`
- `Additional`

`Thinking` is shown incrementally when the upstream API emits reasoning deltas.

## Examples

```text
/set server http://localhost:9083/mcphost/api/v1
/set token
/models
/use 1
/set temperature 0.7
/set top_p 0.95
/set api responses
/image add-file D:\demo\chart.png
Explain transformer KV cache in simple language.
/copy assistant
/copy all
/save
Get-Content prompt.txt | dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --model qwen3.6-plus
Get-Content notes.md | dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- --stdin -m "Summarize the following notes:" --output result.txt
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- -m "Describe this image" --file D:\demo\chart.png --api responses --output answer.txt
dotnet run --project Zhengyan.ChatUI.CLI\Zhengyan.ChatUI.CLI.csproj -- "Summarize MCP in five bullets."
```
