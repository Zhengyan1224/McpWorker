using System.Text;
using System.Runtime.InteropServices;

namespace Zhengyan.ChatUI.CLI.Services;

public sealed class ConsoleInputEditor
{
    private const int StdOutputHandle = -11;
    private const int EnableVirtualTerminalProcessing = 0x0004;
    private static bool? _supportsRelativeRendering;

    public bool CanUseInteractiveConsole => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public string? ReadLine(string prompt, IReadOnlyList<string> history, IReadOnlyList<InputSuggestion>? suggestions = null)
    {
        if (!CanUseInteractiveConsole)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        if (SupportsRelativeRendering())
        {
            return ReadLineRelative(prompt, history, suggestions);
        }

        var text = string.Empty;
        var cursorIndex = 0;
        var historyIndex = history.Count;
        var draftBeforeHistory = string.Empty;
        var originTop = GetCursorTop();
        originTop = PrepareSingleLineViewport(originTop, 9);
        var renderTop = originTop;
        var previousRenderRows = 0;
        var selectedSuggestionIndex = 0;

        void Redraw()
        {
            var renderState = RenderSingleLine(prompt, text, cursorIndex, originTop, renderTop, previousRenderRows, suggestions, selectedSuggestionIndex);
            renderTop = renderState.RenderTop;
            previousRenderRows = renderState.RenderedRows;
        }

        Redraw();

        while (true)
        {
            var activeSuggestions = GetActiveSuggestions(text, suggestions);
            selectedSuggestionIndex = ClampSuggestionIndex(selectedSuggestionIndex, activeSuggestions.Count);
            var key = Console.ReadKey(intercept: true);
            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.A:
                        cursorIndex = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.D:
                        if (text.Length == 0)
                        {
                            ClearRows(renderTop, previousRenderRows);
                            SafeSetCursorPosition(0, GetInputRow(renderTop, previousRenderRows));
                            Console.WriteLine();
                            return null;
                        }

                        if (cursorIndex < text.Length)
                        {
                            var nextIndex = GetNextIndex(text, cursorIndex);
                            text = text.Remove(cursorIndex, nextIndex - cursorIndex);
                            draftBeforeHistory = text;
                            historyIndex = history.Count;
                            selectedSuggestionIndex = 0;
                            Redraw();
                        }

                        continue;
                    case ConsoleKey.E:
                        cursorIndex = text.Length;
                        Redraw();
                        continue;
                    case ConsoleKey.K:
                        text = text[..cursorIndex];
                        draftBeforeHistory = text;
                        historyIndex = history.Count;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.L:
                        text = string.Empty;
                        cursorIndex = 0;
                        historyIndex = history.Count;
                        draftBeforeHistory = string.Empty;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.U:
                        text = text[cursorIndex..];
                        cursorIndex = 0;
                        draftBeforeHistory = text;
                        historyIndex = history.Count;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearRows(renderTop, previousRenderRows);
                    SafeSetCursorPosition(0, GetInputRow(renderTop, previousRenderRows));
                    Console.WriteLine();
                    return text;
                case ConsoleKey.Tab:
                    if (activeSuggestions.Count > 0)
                    {
                        var chosen = activeSuggestions[selectedSuggestionIndex];
                        text = chosen.Command + " ";
                        cursorIndex = text.Length;
                        draftBeforeHistory = text;
                        historyIndex = history.Count;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                    }

                    continue;
                case ConsoleKey.Escape:
                    text = string.Empty;
                    cursorIndex = 0;
                    historyIndex = history.Count;
                    draftBeforeHistory = string.Empty;
                    selectedSuggestionIndex = 0;
                    Redraw();
                    continue;
                case ConsoleKey.LeftArrow:
                    cursorIndex = GetPreviousIndex(text, cursorIndex);
                    Redraw();
                    continue;
                case ConsoleKey.RightArrow:
                    cursorIndex = GetNextIndex(text, cursorIndex);
                    Redraw();
                    continue;
                case ConsoleKey.Home:
                    cursorIndex = 0;
                    Redraw();
                    continue;
                case ConsoleKey.End:
                    cursorIndex = text.Length;
                    Redraw();
                    continue;
                case ConsoleKey.Backspace:
                    if (cursorIndex > 0)
                    {
                        var previousIndex = GetPreviousIndex(text, cursorIndex);
                        text = text.Remove(previousIndex, cursorIndex - previousIndex);
                        cursorIndex = previousIndex;
                        historyIndex = history.Count;
                        draftBeforeHistory = text;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.Delete:
                    if (cursorIndex < text.Length)
                    {
                        var nextIndex = GetNextIndex(text, cursorIndex);
                        text = text.Remove(cursorIndex, nextIndex - cursorIndex);
                        historyIndex = history.Count;
                        draftBeforeHistory = text;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.UpArrow:
                    if (activeSuggestions.Count > 0)
                    {
                        selectedSuggestionIndex = selectedSuggestionIndex > 0 ? selectedSuggestionIndex - 1 : activeSuggestions.Count - 1;
                        Redraw();
                        continue;
                    }

                    if (history.Count > 0 && historyIndex > 0)
                    {
                        if (historyIndex == history.Count)
                        {
                            draftBeforeHistory = text;
                        }

                        historyIndex--;
                        text = history[historyIndex];
                        cursorIndex = text.Length;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.DownArrow:
                    if (activeSuggestions.Count > 0)
                    {
                        selectedSuggestionIndex = (selectedSuggestionIndex + 1) % activeSuggestions.Count;
                        Redraw();
                        continue;
                    }

                    if (history.Count == 0)
                    {
                        continue;
                    }

                    if (historyIndex < history.Count - 1)
                    {
                        historyIndex++;
                        text = history[historyIndex];
                        cursorIndex = text.Length;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }
                    else if (historyIndex == history.Count - 1)
                    {
                        historyIndex = history.Count;
                        text = draftBeforeHistory;
                        cursorIndex = text.Length;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                text = text.Insert(cursorIndex, key.KeyChar.ToString());
                cursorIndex += key.KeyChar.ToString().Length;
                historyIndex = history.Count;
                draftBeforeHistory = text;
                selectedSuggestionIndex = 0;
                Redraw();
            }
        }
    }

    public string? ReadMultiline(string title, string hint, string? initialText = null)
    {
        if (!CanUseInteractiveConsole)
        {
            return ReadMultilineFallback(title, hint, initialText);
        }

        if (SupportsRelativeRendering())
        {
            return ReadMultilineRelative(title, hint, initialText);
        }

        var lines = CreateInitialLines(initialText);
        var cursorLine = lines.Count - 1;
        var cursorColumn = lines[^1].Length;
        var topLine = 0;
        var originTop = GetCursorTop();
        originTop = PrepareMultilineViewport(originTop);
        var previousRenderRows = 0;

        while (true)
        {
            var renderInfo = RenderMultiline(title, hint, lines, cursorLine, cursorColumn, originTop, topLine, previousRenderRows);
            previousRenderRows = renderInfo.RenderedRows;
            topLine = renderInfo.TopLine;

            var key = Console.ReadKey(intercept: true);
            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.A:
                        cursorColumn = 0;
                        continue;
                    case ConsoleKey.D:
                        ClearRows(originTop, previousRenderRows);
                        SafeSetCursorPosition(0, originTop);
                        return string.Join(Environment.NewLine, lines).TrimEnd();
                    case ConsoleKey.E:
                        cursorColumn = lines[cursorLine].Length;
                        continue;
                    case ConsoleKey.K:
                        lines[cursorLine] = lines[cursorLine][..cursorColumn];
                        continue;
                    case ConsoleKey.L:
                        lines = [string.Empty];
                        cursorLine = 0;
                        cursorColumn = 0;
                        topLine = 0;
                        continue;
                    case ConsoleKey.U:
                        lines[cursorLine] = lines[cursorLine][cursorColumn..];
                        cursorColumn = 0;
                        continue;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    ClearRows(originTop, previousRenderRows);
                    SafeSetCursorPosition(0, originTop);
                    return null;
                case ConsoleKey.Enter:
                    InsertNewLine(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.Backspace:
                    Backspace(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.Delete:
                    Delete(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.LeftArrow:
                    MoveLeft(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.RightArrow:
                    MoveRight(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.UpArrow:
                    if (cursorLine > 0)
                    {
                        cursorLine--;
                        cursorColumn = Math.Min(cursorColumn, lines[cursorLine].Length);
                    }

                    break;
                case ConsoleKey.DownArrow:
                    if (cursorLine < lines.Count - 1)
                    {
                        cursorLine++;
                        cursorColumn = Math.Min(cursorColumn, lines[cursorLine].Length);
                    }

                    break;
                case ConsoleKey.Home:
                    cursorColumn = 0;
                    break;
                case ConsoleKey.End:
                    cursorColumn = lines[cursorLine].Length;
                    break;
                case ConsoleKey.Tab:
                    InsertText(lines, ref cursorLine, ref cursorColumn, "    ");
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        InsertText(lines, ref cursorLine, ref cursorColumn, key.KeyChar.ToString());
                    }

                    break;
            }
        }
    }

    private string? ReadLineRelative(string prompt, IReadOnlyList<string> history, IReadOnlyList<InputSuggestion>? suggestions)
    {
        var text = string.Empty;
        var cursorIndex = 0;
        var historyIndex = history.Count;
        var draftBeforeHistory = string.Empty;
        var selectedSuggestionIndex = 0;
        var previousState = RelativeBlockState.Empty;

        void Redraw()
        {
            previousState = RenderSingleLineRelative(prompt, text, cursorIndex, suggestions, selectedSuggestionIndex, previousState);
        }

        Redraw();

        while (true)
        {
            var activeSuggestions = GetActiveSuggestions(text, suggestions);
            selectedSuggestionIndex = ClampSuggestionIndex(selectedSuggestionIndex, activeSuggestions.Count);
            var key = Console.ReadKey(intercept: true);

            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.A:
                        cursorIndex = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.D:
                        if (text.Length == 0)
                        {
                            ClearRelativeBlock(previousState);
                            Console.WriteLine();
                            return null;
                        }

                        if (cursorIndex < text.Length)
                        {
                            var nextIndex = GetNextIndex(text, cursorIndex);
                            text = text.Remove(cursorIndex, nextIndex - cursorIndex);
                            draftBeforeHistory = text;
                            historyIndex = history.Count;
                            selectedSuggestionIndex = 0;
                            Redraw();
                        }

                        continue;
                    case ConsoleKey.E:
                        cursorIndex = text.Length;
                        Redraw();
                        continue;
                    case ConsoleKey.K:
                        text = text[..cursorIndex];
                        draftBeforeHistory = text;
                        historyIndex = history.Count;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.L:
                        text = string.Empty;
                        cursorIndex = 0;
                        historyIndex = history.Count;
                        draftBeforeHistory = string.Empty;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.U:
                        text = text[cursorIndex..];
                        cursorIndex = 0;
                        draftBeforeHistory = text;
                        historyIndex = history.Count;
                        selectedSuggestionIndex = 0;
                        Redraw();
                        continue;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearRelativeBlock(previousState);
                    Console.WriteLine();
                    return text;
                case ConsoleKey.Tab:
                    if (activeSuggestions.Count > 0)
                    {
                        var chosen = activeSuggestions[selectedSuggestionIndex];
                        text = chosen.Command + " ";
                        cursorIndex = text.Length;
                        draftBeforeHistory = text;
                        historyIndex = history.Count;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.Escape:
                    text = string.Empty;
                    cursorIndex = 0;
                    historyIndex = history.Count;
                    draftBeforeHistory = string.Empty;
                    selectedSuggestionIndex = 0;
                    Redraw();
                    continue;
                case ConsoleKey.LeftArrow:
                    cursorIndex = GetPreviousIndex(text, cursorIndex);
                    Redraw();
                    continue;
                case ConsoleKey.RightArrow:
                    cursorIndex = GetNextIndex(text, cursorIndex);
                    Redraw();
                    continue;
                case ConsoleKey.Home:
                    cursorIndex = 0;
                    Redraw();
                    continue;
                case ConsoleKey.End:
                    cursorIndex = text.Length;
                    Redraw();
                    continue;
                case ConsoleKey.Backspace:
                    if (cursorIndex > 0)
                    {
                        var previousIndex = GetPreviousIndex(text, cursorIndex);
                        text = text.Remove(previousIndex, cursorIndex - previousIndex);
                        cursorIndex = previousIndex;
                        historyIndex = history.Count;
                        draftBeforeHistory = text;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.Delete:
                    if (cursorIndex < text.Length)
                    {
                        var nextIndex = GetNextIndex(text, cursorIndex);
                        text = text.Remove(cursorIndex, nextIndex - cursorIndex);
                        historyIndex = history.Count;
                        draftBeforeHistory = text;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.UpArrow:
                    if (activeSuggestions.Count > 0)
                    {
                        selectedSuggestionIndex = selectedSuggestionIndex > 0 ? selectedSuggestionIndex - 1 : activeSuggestions.Count - 1;
                        Redraw();
                        continue;
                    }

                    if (history.Count > 0 && historyIndex > 0)
                    {
                        if (historyIndex == history.Count)
                        {
                            draftBeforeHistory = text;
                        }

                        historyIndex--;
                        text = history[historyIndex];
                        cursorIndex = text.Length;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
                case ConsoleKey.DownArrow:
                    if (activeSuggestions.Count > 0)
                    {
                        selectedSuggestionIndex = (selectedSuggestionIndex + 1) % activeSuggestions.Count;
                        Redraw();
                        continue;
                    }

                    if (history.Count == 0)
                    {
                        continue;
                    }

                    if (historyIndex < history.Count - 1)
                    {
                        historyIndex++;
                        text = history[historyIndex];
                        cursorIndex = text.Length;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }
                    else if (historyIndex == history.Count - 1)
                    {
                        historyIndex = history.Count;
                        text = draftBeforeHistory;
                        cursorIndex = text.Length;
                        selectedSuggestionIndex = 0;
                        Redraw();
                    }

                    continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                text = text.Insert(cursorIndex, key.KeyChar.ToString());
                cursorIndex += key.KeyChar.ToString().Length;
                historyIndex = history.Count;
                draftBeforeHistory = text;
                selectedSuggestionIndex = 0;
                Redraw();
            }
        }
    }

    private string? ReadMultilineRelative(string title, string hint, string? initialText)
    {
        var lines = CreateInitialLines(initialText);
        var cursorLine = lines.Count - 1;
        var cursorColumn = lines[^1].Length;
        var topLine = 0;
        var previousState = RelativeBlockState.Empty;

        void Redraw()
        {
            var render = RenderMultilineRelative(title, hint, lines, cursorLine, cursorColumn, topLine, previousState);
            previousState = render.State;
            topLine = render.TopLine;
        }

        Redraw();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if ((key.Modifiers & ConsoleModifiers.Control) != 0)
            {
                switch (key.Key)
                {
                    case ConsoleKey.A:
                        cursorColumn = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.D:
                        ClearRelativeBlock(previousState);
                        return string.Join(Environment.NewLine, lines).TrimEnd();
                    case ConsoleKey.E:
                        cursorColumn = lines[cursorLine].Length;
                        Redraw();
                        continue;
                    case ConsoleKey.K:
                        lines[cursorLine] = lines[cursorLine][..cursorColumn];
                        Redraw();
                        continue;
                    case ConsoleKey.L:
                        lines = [string.Empty];
                        cursorLine = 0;
                        cursorColumn = 0;
                        topLine = 0;
                        Redraw();
                        continue;
                    case ConsoleKey.U:
                        lines[cursorLine] = lines[cursorLine][cursorColumn..];
                        cursorColumn = 0;
                        Redraw();
                        continue;
                }
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    ClearRelativeBlock(previousState);
                    return null;
                case ConsoleKey.Enter:
                    InsertNewLine(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.Backspace:
                    Backspace(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.Delete:
                    Delete(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.LeftArrow:
                    MoveLeft(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.RightArrow:
                    MoveRight(lines, ref cursorLine, ref cursorColumn);
                    break;
                case ConsoleKey.UpArrow:
                    if (cursorLine > 0)
                    {
                        cursorLine--;
                        cursorColumn = Math.Min(cursorColumn, lines[cursorLine].Length);
                    }

                    break;
                case ConsoleKey.DownArrow:
                    if (cursorLine < lines.Count - 1)
                    {
                        cursorLine++;
                        cursorColumn = Math.Min(cursorColumn, lines[cursorLine].Length);
                    }

                    break;
                case ConsoleKey.Home:
                    cursorColumn = 0;
                    break;
                case ConsoleKey.End:
                    cursorColumn = lines[cursorLine].Length;
                    break;
                case ConsoleKey.Tab:
                    InsertText(lines, ref cursorLine, ref cursorColumn, "    ");
                    break;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        InsertText(lines, ref cursorLine, ref cursorColumn, key.KeyChar.ToString());
                    }

                    break;
            }

            Redraw();
        }
    }

    private static string? ReadMultilineFallback(string title, string hint, string? initialText)
    {
        Console.WriteLine(title);
        Console.WriteLine(hint);
        Console.WriteLine("Fallback mode: finish with a single '.' line.");

        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(initialText))
        {
            builder.Append(initialText.TrimEnd());
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }
        }

        while (true)
        {
            var line = Console.ReadLine();
            if (line is null || line == ".")
            {
                break;
            }

            builder.AppendLine(line);
        }

        return builder.ToString().TrimEnd();
    }

    private static List<string> CreateInitialLines(string? initialText)
    {
        if (string.IsNullOrEmpty(initialText))
        {
            return [string.Empty];
        }

        return initialText.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .ToList();
    }

    private static RelativeBlockState RenderSingleLineRelative(
        string prompt,
        string text,
        int cursorIndex,
        IReadOnlyList<InputSuggestion>? suggestions,
        int selectedSuggestionIndex,
        RelativeBlockState previousState)
    {
        var width = GetWindowWidth();
        var activeSuggestions = GetActiveSuggestions(text, suggestions);
        var renderedSuggestionCount = Math.Min(8, activeSuggestions.Count);

        var promptWidth = MeasureDisplayWidth(prompt);
        var availableWidth = Math.Max(8, width - promptWidth - 1);
        var visibleText = BuildVisibleText(text, cursorIndex, availableWidth, true);

        var lines = new List<string>(1 + renderedSuggestionCount)
        {
            TruncateToWidth(prompt + visibleText.Text, width - 1)
        };

        var commandWidth = Math.Clamp(width / 3, 16, 28);
        for (var index = 0; index < renderedSuggestionCount; index++)
        {
            var suggestion = activeSuggestions[index];
            var marker = index == ClampSuggestionIndex(selectedSuggestionIndex, activeSuggestions.Count) ? ">" : " ";
            var commandText = $"  {marker} {suggestion.Command}";
            var descriptionWidth = Math.Max(10, width - commandWidth - 1);
            var line = commandText.PadRight(commandWidth) + TruncateToWidth(suggestion.Description, descriptionWidth);
            lines.Add(TruncateToWidth(line, width - 1));
        }

        return RenderRelativeBlock(lines, 0, Math.Min(width - 2, promptWidth + visibleText.CursorDisplayOffset), previousState);
    }

    private static MultilineRelativeRenderResult RenderMultilineRelative(
        string title,
        string hint,
        IReadOnlyList<string> lines,
        int cursorLine,
        int cursorColumn,
        int requestedTopLine,
        RelativeBlockState previousState)
    {
        var width = GetWindowWidth();
        const int statusRows = 2;
        const int frameRows = 2;
        const int footerRows = 1;
        const int targetVisibleRows = 10;
        var chromeRows = statusRows + frameRows + footerRows;
        var visibleRows = Math.Max(4, Math.Min(targetVisibleRows, GetWindowHeight() - chromeRows - 1));
        visibleRows = Math.Max(1, visibleRows);

        var topLine = requestedTopLine;
        if (cursorLine < topLine)
        {
            topLine = cursorLine;
        }

        if (cursorLine >= topLine + visibleRows)
        {
            topLine = cursorLine - visibleRows + 1;
        }

        var currentLine = cursorLine < lines.Count ? lines[cursorLine] : string.Empty;
        var currentColumn = Math.Min(currentLine.Length, Math.Max(0, cursorColumn));
        var positionSummary = $"Ln {cursorLine + 1}/{lines.Count}  Col {MeasureDisplayWidth(currentLine[..currentColumn]) + 1}";

        var panelWidth = Math.Max(12, width - 1);
        var innerWidth = Math.Max(8, panelWidth - 2);
        var gutterWidth = Math.Min(8, Math.Max(6, innerWidth / 4));
        var contentWidth = Math.Max(4, innerWidth - gutterWidth);

        var renderLines = new List<string>(statusRows + visibleRows + footerRows + frameRows)
        {
            PadToWidth(" MULTILINE DRAFT ", width - 1),
            PadToWidth(TruncateToWidth($"{title} | {positionSummary}", width - 1), width - 1),
            BuildFrameBorder("+", "+", innerWidth, "editor")
        };

        var cursorRow = statusRows + 1;
        var cursorLeft = 1 + gutterWidth;

        for (var row = 0; row < visibleRows; row++)
        {
            var lineIndex = topLine + row;
            if (lineIndex >= lines.Count)
            {
                renderLines.Add($"|{new string(' ', innerWidth)}|");
                continue;
            }

            var isCurrentLine = lineIndex == cursorLine;
            var marker = isCurrentLine ? ">" : " ";
            var gutter = $"{marker}{lineIndex + 1,4} |";
            var visibleText = BuildVisibleText(lines[lineIndex], isCurrentLine ? cursorColumn : lines[lineIndex].Length, contentWidth, isCurrentLine);
            renderLines.Add($"|{PadToWidth(gutter + visibleText.Text, innerWidth)}|");

            if (isCurrentLine)
            {
                cursorRow = statusRows + 1 + row;
                cursorLeft = Math.Min(width - 2, 1 + gutterWidth + visibleText.CursorDisplayOffset);
            }
        }

        var visibleStart = Math.Min(lines.Count, topLine + 1);
        var visibleEnd = Math.Min(lines.Count, topLine + visibleRows);
        var footerText = lines.Count <= visibleRows
            ? $" {hint}"
            : $" {hint} | View {visibleStart}-{visibleEnd}/{lines.Count}";
        renderLines.Add($"|{PadToWidth(footerText, innerWidth)}|");
        renderLines.Add(BuildFrameBorder("+", "+", innerWidth, "draft"));

        var state = RenderRelativeBlock(renderLines, cursorRow, cursorLeft, previousState, statusRows);
        return new MultilineRelativeRenderResult(state, topLine);
    }

    private static MultilineRenderInfo RenderMultiline(
        string title,
        string hint,
        IReadOnlyList<string> lines,
        int cursorLine,
        int cursorColumn,
        int originTop,
        int requestedTopLine,
        int previousRenderRows)
    {
        var width = GetWindowWidth();
        const int statusRows = 2;
        const int frameRows = 2;
        const int footerRows = 1;
        const int targetVisibleRows = 10;
        const int minimumVisibleRows = 4;
        var height = GetWindowHeight();
        var chromeRows = statusRows + frameRows + footerRows;
        var availableWindowRows = GetVisibleRows(originTop, height);
        var maxVisibleRows = Math.Max(1, availableWindowRows - chromeRows);
        var visibleRows = maxVisibleRows >= minimumVisibleRows
            ? Math.Min(targetVisibleRows, maxVisibleRows)
            : maxVisibleRows;
        var topLine = requestedTopLine;
        if (cursorLine < topLine)
        {
            topLine = cursorLine;
        }

        if (cursorLine >= topLine + visibleRows)
        {
            topLine = cursorLine - visibleRows + 1;
        }

        var renderedRows = chromeRows + visibleRows;
        EnsureRenderArea(originTop, renderedRows);
        renderedRows = Math.Max(1, GetRenderableRows(originTop, renderedRows));
        visibleRows = Math.Max(1, renderedRows - chromeRows);
        if (cursorLine < topLine)
        {
            topLine = cursorLine;
        }

        if (cursorLine >= topLine + visibleRows)
        {
            topLine = Math.Max(0, cursorLine - visibleRows + 1);
        }

        ClearRows(originTop, Math.Max(previousRenderRows, renderedRows));

        var currentLine = cursorLine < lines.Count ? lines[cursorLine] : string.Empty;
        var currentColumn = Math.Min(currentLine.Length, Math.Max(0, cursorColumn));
        var positionSummary = $"Ln {cursorLine + 1}/{lines.Count}  Col {MeasureDisplayWidth(currentLine[..currentColumn]) + 1}";
        WriteStatusLine(originTop, " MULTILINE DRAFT ", ConsoleColor.Black, ConsoleColor.DarkCyan);
        WriteStatusLine(originTop + 1, TruncateToWidth($"{title} | {positionSummary}", width - 1), ConsoleColor.Gray, ConsoleColor.DarkGray);

        var frameTop = originTop + statusRows;
        var frameBottom = frameTop + visibleRows + footerRows + frameRows - 1;
        var panelWidth = Math.Max(12, width - 1);
        var innerWidth = Math.Max(8, panelWidth - 2);
        WriteClippedLine(frameTop, BuildFrameBorder("┌", "┐", innerWidth, " editor "));

        var gutterWidth = Math.Min(8, Math.Max(6, innerWidth / 4));
        var contentWidth = Math.Max(4, innerWidth - gutterWidth);
        var cursorLeft = 1 + gutterWidth;
        var cursorTop = frameTop + 1;

        for (var row = 0; row < visibleRows; row++)
        {
            var lineIndex = topLine + row;
            var targetTop = frameTop + 1 + row;
            if (lineIndex >= lines.Count)
            {
                WritePanelLine(targetTop, string.Empty, innerWidth);
                continue;
            }

            var isCurrentLine = lineIndex == cursorLine;
            var marker = isCurrentLine ? "›" : " ";
            var gutter = $"{marker}{lineIndex + 1,4} │";
            var visibleText = BuildVisibleText(lines[lineIndex], isCurrentLine ? cursorColumn : lines[lineIndex].Length, contentWidth, isCurrentLine);
            WritePanelLine(targetTop, gutter + visibleText.Text, innerWidth);

            if (isCurrentLine)
            {
                cursorLeft = Math.Min(width - 2, 1 + gutterWidth + visibleText.CursorDisplayOffset);
                cursorTop = targetTop;
            }
        }

        var visibleStart = Math.Min(lines.Count, topLine + 1);
        var visibleEnd = Math.Min(lines.Count, topLine + visibleRows);
        var footerText = lines.Count <= visibleRows
            ? $" {hint}"
            : $" {hint} | View {visibleStart}-{visibleEnd}/{lines.Count}";
        WritePanelLine(frameBottom - 1, footerText, innerWidth);
        WriteClippedLine(frameBottom, BuildFrameBorder("└", "┘", innerWidth, " draft "));

        SafeSetCursorPosition(cursorLeft, cursorTop);
        return new MultilineRenderInfo(renderedRows, topLine);
    }

    private static SingleLineRenderInfo RenderSingleLine(
        string prompt,
        string text,
        int cursorIndex,
        int originTop,
        int previousRenderTop,
        int previousRenderRows,
        IReadOnlyList<InputSuggestion>? suggestions,
        int selectedSuggestionIndex)
    {
        var width = GetWindowWidth();
        var activeSuggestions = GetActiveSuggestions(text, suggestions);
        var renderedSuggestionCount = Math.Min(8, activeSuggestions.Count);
        var renderedRows = 1 + renderedSuggestionCount;
        var renderTop = ShouldRenderSuggestionsAbovePrompt() && renderedSuggestionCount > 0
            ? Math.Max(0, originTop - renderedSuggestionCount)
            : originTop;

        EnsureRenderArea(renderTop, renderedRows);
        renderedRows = Math.Max(1, GetRenderableRows(renderTop, renderedRows));
        renderedSuggestionCount = Math.Max(0, renderedRows - 1);
        var renderSuggestionsAbove = ShouldRenderSuggestionsAbovePrompt() && renderedSuggestionCount > 0;
        var clearTop = Math.Min(previousRenderTop, renderTop);
        var clearBottom = Math.Max(previousRenderTop + Math.Max(0, previousRenderRows - 1), renderTop + Math.Max(0, renderedRows - 1));
        ClearRows(clearTop, clearBottom - clearTop + 1);

        var promptWidth = MeasureDisplayWidth(prompt);
        var availableWidth = Math.Max(8, width - promptWidth - 1);
        var visibleText = BuildVisibleText(text, cursorIndex, availableWidth, true);
        var lineText = prompt + visibleText.Text;
        var promptTop = renderSuggestionsAbove ? renderTop + renderedSuggestionCount : renderTop;
        WriteClippedLine(promptTop, TruncateToWidth(lineText, width - 1));

        var commandWidth = Math.Clamp(width / 3, 16, 28);
        for (var index = 0; index < renderedSuggestionCount; index++)
        {
            var suggestion = activeSuggestions[index];
            var marker = index == ClampSuggestionIndex(selectedSuggestionIndex, activeSuggestions.Count) ? ">" : " ";
            var commandText = $"  {marker} {suggestion.Command}";
            var descriptionWidth = Math.Max(10, width - commandWidth - 1);
            var line = commandText.PadRight(commandWidth) + TruncateToWidth(suggestion.Description, descriptionWidth);
            var suggestionTop = renderSuggestionsAbove
                ? renderTop + index
                : renderTop + 1 + index;
            WriteClippedLine(suggestionTop, TruncateToWidth(line, width - 1));
        }

        var cursorLeft = Math.Min(width - 2, promptWidth + visibleText.CursorDisplayOffset);
        SafeSetCursorPosition(cursorLeft, promptTop);
        return new SingleLineRenderInfo(renderTop, renderedRows);
    }

    private static List<InputSuggestion> GetActiveSuggestions(string text, IReadOnlyList<InputSuggestion>? suggestions)
    {
        if (suggestions is not { Count: > 0 })
        {
            return [];
        }

        if (!IsSlashCommandQuery(text, out var query))
        {
            return [];
        }

        return suggestions
            .Where(item => string.IsNullOrEmpty(query)
                || item.Command[1..].StartsWith(query, StringComparison.OrdinalIgnoreCase)
                || item.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Command[1..].StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Command.Length)
            .ThenBy(item => item.Command, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsSlashCommandQuery(string text, out string query)
    {
        query = string.Empty;
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (text.Contains(' '))
        {
            return false;
        }

        query = text[1..];
        return true;
    }

    private static int ClampSuggestionIndex(int selectedSuggestionIndex, int suggestionCount)
    {
        if (suggestionCount <= 0)
        {
            return 0;
        }

        return Math.Clamp(selectedSuggestionIndex, 0, suggestionCount - 1);
    }

    private static VisibleText BuildVisibleText(string text, int cursorIndex, int availableWidth, bool keepCursorVisible)
    {
        if (availableWidth <= 0)
        {
            return new VisibleText(string.Empty, 0);
        }

        var startIndex = 0;
        if (keepCursorVisible)
        {
            startIndex = FindVisibleStartIndex(text, cursorIndex, availableWidth);
        }

        const string ellipsis = "...";
        var leftMarker = startIndex > 0 ? ellipsis : string.Empty;
        var contentWidth = availableWidth - MeasureDisplayWidth(leftMarker);
        var content = TakeByWidth(text, startIndex, contentWidth, out var consumedChars);
        var endIndex = startIndex + consumedChars;

        if (endIndex < text.Length)
        {
            contentWidth = Math.Max(0, availableWidth - MeasureDisplayWidth(leftMarker) - MeasureDisplayWidth(ellipsis));
            content = TakeByWidth(text, startIndex, contentWidth, out consumedChars);
            endIndex = startIndex + consumedChars;
        }

        var rightMarker = endIndex < text.Length ? ellipsis : string.Empty;
        var cursorText = cursorIndex <= startIndex
            ? string.Empty
            : text.Substring(startIndex, Math.Min(cursorIndex, endIndex) - startIndex);
        var cursorDisplayOffset = MeasureDisplayWidth(leftMarker) + MeasureDisplayWidth(cursorText);
        return new VisibleText(leftMarker + content + rightMarker, cursorDisplayOffset);
    }

    private static int FindVisibleStartIndex(string text, int cursorIndex, int availableWidth)
    {
        if (string.IsNullOrEmpty(text) || cursorIndex <= 0)
        {
            return 0;
        }

        var visibleWidth = Math.Max(1, availableWidth - 3);
        var startIndex = cursorIndex;
        while (startIndex > 0)
        {
            var previousIndex = GetPreviousIndex(text, startIndex);
            var sliceWidth = MeasureDisplayWidth(text.Substring(previousIndex, cursorIndex - previousIndex));
            if (sliceWidth > visibleWidth)
            {
                break;
            }

            startIndex = previousIndex;
        }

        return startIndex;
    }

    private static void InsertText(IList<string> lines, ref int cursorLine, ref int cursorColumn, string text)
    {
        lines[cursorLine] = lines[cursorLine].Insert(cursorColumn, text);
        cursorColumn += text.Length;
    }

    private static void InsertNewLine(IList<string> lines, ref int cursorLine, ref int cursorColumn)
    {
        var currentLine = lines[cursorLine];
        var before = currentLine[..cursorColumn];
        var after = currentLine[cursorColumn..];
        lines[cursorLine] = before;
        lines.Insert(cursorLine + 1, after);
        cursorLine++;
        cursorColumn = 0;
    }

    private static void Backspace(IList<string> lines, ref int cursorLine, ref int cursorColumn)
    {
        if (cursorColumn > 0)
        {
            var previousIndex = GetPreviousIndex(lines[cursorLine], cursorColumn);
            lines[cursorLine] = lines[cursorLine].Remove(previousIndex, cursorColumn - previousIndex);
            cursorColumn = previousIndex;
            return;
        }

        if (cursorLine == 0)
        {
            return;
        }

        var previousLineLength = lines[cursorLine - 1].Length;
        lines[cursorLine - 1] += lines[cursorLine];
        lines.RemoveAt(cursorLine);
        cursorLine--;
        cursorColumn = previousLineLength;
    }

    private static void Delete(IList<string> lines, ref int cursorLine, ref int cursorColumn)
    {
        if (cursorColumn < lines[cursorLine].Length)
        {
            var nextIndex = GetNextIndex(lines[cursorLine], cursorColumn);
            lines[cursorLine] = lines[cursorLine].Remove(cursorColumn, nextIndex - cursorColumn);
            return;
        }

        if (cursorLine >= lines.Count - 1)
        {
            return;
        }

        lines[cursorLine] += lines[cursorLine + 1];
        lines.RemoveAt(cursorLine + 1);
    }

    private static void MoveLeft(IReadOnlyList<string> lines, ref int cursorLine, ref int cursorColumn)
    {
        if (cursorColumn > 0)
        {
            cursorColumn = GetPreviousIndex(lines[cursorLine], cursorColumn);
            return;
        }

        if (cursorLine == 0)
        {
            return;
        }

        cursorLine--;
        cursorColumn = lines[cursorLine].Length;
    }

    private static void MoveRight(IReadOnlyList<string> lines, ref int cursorLine, ref int cursorColumn)
    {
        if (cursorColumn < lines[cursorLine].Length)
        {
            cursorColumn = GetNextIndex(lines[cursorLine], cursorColumn);
            return;
        }

        if (cursorLine >= lines.Count - 1)
        {
            return;
        }

        cursorLine++;
        cursorColumn = 0;
    }

    private static RelativeBlockState RenderRelativeBlock(
        IReadOnlyList<string> lines,
        int cursorRow,
        int cursorLeft,
        RelativeBlockState previousState,
        int protectedTopRows = 0)
    {
        MoveToRelativeBlockTop(previousState);

        var maxRows = Math.Max(previousState.RenderedRows, lines.Count);
        for (var row = 0; row < maxRows; row++)
        {
            ClearCurrentLine();
            if (row < lines.Count)
            {
                Console.Write(lines[row]);
            }

            if (row < maxRows - 1)
            {
                Console.WriteLine();
            }
        }

        if (previousState.RenderedRows > lines.Count)
        {
            MoveToRowFromBlockBottom(maxRows, lines.Count);
            DeleteLines(previousState.RenderedRows - lines.Count);
            maxRows = lines.Count;
        }

        MoveToRowFromBlockBottom(maxRows, cursorRow);
        SetCursorColumn(cursorLeft);
        return new RelativeBlockState(lines.Count, cursorRow, protectedTopRows);
    }

    private static void ClearRelativeBlock(RelativeBlockState state)
    {
        if (state.RenderedRows <= 0)
        {
            return;
        }

        MoveToRelativeBlockTop(state);
        DeleteLines(state.RenderedRows);
    }

    private static void MoveToRelativeBlockTop(RelativeBlockState state)
    {
        if (state.RenderedRows <= 0)
        {
            return;
        }

        Console.Write('\r');
        if (state.CursorRow > 0)
        {
            Console.Write($"\u001b[{state.CursorRow}A");
        }
    }

    private static void MoveToRowFromBlockBottom(int totalRows, int targetRow)
    {
        var rowsUp = Math.Max(0, totalRows - 1 - targetRow);
        Console.Write('\r');
        if (rowsUp > 0)
        {
            Console.Write($"\u001b[{rowsUp}A");
        }
    }

    private static void ClearCurrentLine()
    {
        Console.Write("\u001b[2K\r");
    }

    private static void DeleteLines(int lineCount)
    {
        if (lineCount <= 0)
        {
            return;
        }

        Console.Write($"\u001b[{lineCount}M");
    }

    private static void SetCursorColumn(int left)
    {
        Console.Write($"\u001b[{Math.Max(1, left + 1)}G");
    }

    private static void ClearRows(int originTop, int rowCount)
    {
        var width = GetWindowWidth();
        var blank = new string(' ', Math.Max(0, width - 1));
        var maxRows = GetAvailableRows(originTop, rowCount);
        for (var row = 0; row < maxRows; row++)
        {
            SafeSetCursorPosition(0, originTop + row);
            Console.Write(blank);
        }
    }

    private static void WriteClippedLine(int top, string text)
    {
        if (top < 0 || top >= GetBufferHeight())
        {
            return;
        }

        SafeSetCursorPosition(0, top);
        Console.Write(text);
    }

    private static void WriteStatusLine(int top, string text, ConsoleColor foreground, ConsoleColor background)
    {
        if (top < 0 || top >= GetBufferHeight())
        {
            return;
        }

        var width = Math.Max(1, GetWindowWidth() - 1);
        var previousForeground = Console.ForegroundColor;
        var previousBackground = Console.BackgroundColor;

        try
        {
            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;
            SafeSetCursorPosition(0, top);
            Console.Write(PadToWidth(text, width));
        }
        finally
        {
            Console.ForegroundColor = previousForeground;
            Console.BackgroundColor = previousBackground;
        }
    }

    private static void WritePanelLine(int top, string text, int innerWidth)
    {
        WriteClippedLine(top, $"│{PadToWidth(text, innerWidth)}│");
    }

    private static string TruncateToWidth(string text, int maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        var content = TakeByWidth(text, 0, maxWidth, out var consumedChars);
        if (consumedChars >= text.Length)
        {
            return content;
        }

        const string ellipsis = "...";
        var shortened = TakeByWidth(text, 0, Math.Max(0, maxWidth - MeasureDisplayWidth(ellipsis)), out _);
        return shortened + ellipsis;
    }

    private static string PadToWidth(string text, int width)
    {
        var truncated = TruncateToWidth(text, width);
        var padding = Math.Max(0, width - MeasureDisplayWidth(truncated));
        return truncated + new string(' ', padding);
    }

    private static string BuildFrameBorder(string left, string right, int innerWidth, string label)
    {
        var normalizedLabel = string.IsNullOrWhiteSpace(label) ? string.Empty : $" {label.Trim()} ";
        var labelWidth = Math.Min(innerWidth, MeasureDisplayWidth(normalizedLabel));
        var visibleLabel = labelWidth > 0 ? TakeByWidth(normalizedLabel, 0, labelWidth, out _) : string.Empty;
        var remainingWidth = Math.Max(0, innerWidth - MeasureDisplayWidth(visibleLabel));
        var leftWidth = remainingWidth / 2;
        var rightWidth = remainingWidth - leftWidth;
        return left + new string('─', leftWidth) + visibleLabel + new string('─', rightWidth) + right;
    }

    private static string TakeByWidth(string text, int startIndex, int maxWidth, out int consumedChars)
    {
        if (maxWidth <= 0 || startIndex >= text.Length)
        {
            consumedChars = 0;
            return string.Empty;
        }

        var builder = new StringBuilder();
        var width = 0;
        var index = startIndex;
        while (index < text.Length)
        {
            var rune = Rune.GetRuneAt(text, index);
            var runeWidth = GetRuneWidth(rune);
            if (width + runeWidth > maxWidth)
            {
                break;
            }

            builder.Append(rune.ToString());
            width += runeWidth;
            index += rune.Utf16SequenceLength;
        }

        consumedChars = index - startIndex;
        return builder.ToString();
    }

    private static int MeasureDisplayWidth(string text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += GetRuneWidth(rune);
        }

        return width;
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

    private static int GetPreviousIndex(string text, int index)
    {
        if (index <= 0)
        {
            return 0;
        }

        index--;
        if (index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]))
        {
            index--;
        }

        return index;
    }

    private static int GetNextIndex(string text, int index)
    {
        if (index >= text.Length)
        {
            return text.Length;
        }

        var step = char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;
        return Math.Min(text.Length, index + step);
    }

    private static int GetWindowWidth()
    {
        try
        {
            return Math.Max(40, Console.WindowWidth);
        }
        catch
        {
            return 120;
        }
    }

    private static int GetWindowHeight()
    {
        try
        {
            return Math.Max(10, Console.WindowHeight);
        }
        catch
        {
            return 30;
        }
    }

    private static int GetBufferHeight()
    {
        try
        {
            return Math.Max(1, Console.BufferHeight);
        }
        catch
        {
            return Math.Max(30, GetWindowHeight());
        }
    }

    private static int GetBufferWidth()
    {
        try
        {
            return Math.Max(1, Console.BufferWidth);
        }
        catch
        {
            return GetWindowWidth();
        }
    }

    private static int GetCursorTop()
    {
        try
        {
            return Math.Clamp(Console.CursorTop, 0, GetBufferHeight() - 1);
        }
        catch
        {
            return 0;
        }
    }

    private static int GetWindowTop()
    {
        var inferredWindowTop = Math.Max(0, GetCursorTop() - GetWindowHeight() + 1);
        try
        {
            return Math.Max(Math.Max(0, Console.WindowTop), inferredWindowTop);
        }
        catch
        {
            return inferredWindowTop;
        }
    }

    private static bool SupportsRelativeRendering()
    {
        if (_supportsRelativeRendering.HasValue)
        {
            return _supportsRelativeRendering.Value;
        }

        if (!CanUseAnsiSequences())
        {
            _supportsRelativeRendering = false;
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            _supportsRelativeRendering = true;
            return true;
        }

        try
        {
            var handle = GetStdHandle(StdOutputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                _supportsRelativeRendering = false;
                return false;
            }

            if (!GetConsoleMode(handle, out var mode))
            {
                _supportsRelativeRendering = false;
                return false;
            }

            if ((mode & EnableVirtualTerminalProcessing) == 0)
            {
                if (!SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing))
                {
                    _supportsRelativeRendering = false;
                    return false;
                }
            }

            _supportsRelativeRendering = true;
            return true;
        }
        catch
        {
            _supportsRelativeRendering = false;
            return false;
        }
    }

    private static bool CanUseAnsiSequences()
    {
        return !Console.IsOutputRedirected
            && (!OperatingSystem.IsWindows()
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TERM"))
                || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConEmuANSI")));
    }

    private static bool ShouldRenderSuggestionsAbovePrompt()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION"));
    }

    private static bool ShouldPinMultilineViewport()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WT_SESSION"));
    }

    private static int PrepareSingleLineViewport(int originTop, int desiredRows)
    {
        return PrepareViewport(originTop, desiredRows);
    }

    private static int PrepareMultilineViewport(int originTop)
    {
        const int targetTotalRows = 15;
        var desiredRows = Math.Max(7, Math.Min(targetTotalRows, GetWindowHeight() - 1));
        if (ShouldPinMultilineViewport())
        {
            EnsureRenderArea(originTop, desiredRows);
            try
            {
                SafeSetCursorPosition(0, GetCursorTop());
                for (var index = 0; index < desiredRows - 1; index++)
                {
                    Console.WriteLine();
                }
            }
            catch
            {
                return originTop;
            }

            return Math.Max(0, GetCursorTop() - desiredRows + 1);
        }

        return PrepareViewport(originTop, desiredRows);
    }

    private static int PrepareViewport(int originTop, int desiredRows)
    {
        desiredRows = Math.Max(1, Math.Min(desiredRows, GetWindowHeight()));
        EnsureRenderArea(originTop, desiredRows);
        var missingRows = desiredRows - GetVisibleRows(originTop, desiredRows);
        if (missingRows <= 0)
        {
            return originTop;
        }

        try
        {
            SafeSetCursorPosition(0, Math.Max(originTop, GetCursorTop()));
            for (var index = 0; index < missingRows; index++)
            {
                Console.WriteLine();
            }
        }
        catch
        {
            return originTop;
        }

        return Math.Max(0, GetCursorTop() - missingRows);
    }

    private static void EnsureRenderArea(int originTop, int requiredRows)
    {
        var requiredBottom = originTop + Math.Max(1, requiredRows) - 1;
        try
        {
            if (OperatingSystem.IsWindows() && requiredBottom >= Console.BufferHeight)
            {
                var targetHeight = requiredBottom + 1;
                if (targetHeight >= Console.WindowHeight)
                {
                    Console.BufferHeight = targetHeight;
                }
            }
        }
        catch
        {
        }
    }

    private static int GetAvailableRows(int originTop, int requestedRows)
    {
        var available = GetBufferHeight() - originTop;
        return Math.Max(1, Math.Min(requestedRows, available));
    }

    private static int GetVisibleRows(int originTop, int requestedRows)
    {
        var windowBottom = GetWindowTop() + GetWindowHeight() - 1;
        var available = windowBottom - originTop + 1;
        return Math.Max(1, Math.Min(requestedRows, available));
    }

    private static int GetRenderableRows(int originTop, int requestedRows)
    {
        return Math.Max(1, Math.Min(GetAvailableRows(originTop, requestedRows), GetVisibleRows(originTop, requestedRows)));
    }

    private static void SafeSetCursorPosition(int left, int top)
    {
        try
        {
            var safeTop = Math.Clamp(top, 0, GetBufferHeight() - 1);
            var safeLeft = Math.Clamp(left, 0, GetBufferWidth() - 1);
            Console.SetCursorPosition(safeLeft, safeTop);
        }
        catch
        {
        }
    }

    private readonly record struct VisibleText(string Text, int CursorDisplayOffset);

    private readonly record struct MultilineRenderInfo(int RenderedRows, int TopLine);

    private readonly record struct SingleLineRenderInfo(int RenderTop, int RenderedRows);

    private readonly record struct RelativeBlockState(int RenderedRows, int CursorRow, int ProtectedTopRows)
    {
        public static RelativeBlockState Empty => new(0, 0, 0);
    }

    private readonly record struct MultilineRelativeRenderResult(RelativeBlockState State, int TopLine);

    private static int GetInputRow(int renderTop, int renderedRows)
    {
        return ShouldRenderSuggestionsAbovePrompt() && renderedRows > 1
            ? renderTop + renderedRows - 1
            : renderTop;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
}

public sealed record InputSuggestion(string Command, string Description);
