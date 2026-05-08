using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Zhengyan.ChatUI.Desktop.Controls;

public partial class MarkdownMessageView : UserControl
{
    public static readonly StyledProperty<string?> MarkdownTextProperty =
        AvaloniaProperty.Register<MarkdownMessageView, string?>(nameof(MarkdownText));

    private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListRegex = new(@"^\s*[-*]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new(@"^\s*(\d+)\.\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex BlockQuoteRegex = new(@"^\s*>\s?(.*)$", RegexOptions.Compiled);
    private static readonly Regex HorizontalRuleRegex = new(@"^\s{0,3}([-*_])(?:\s*\1){2,}\s*$", RegexOptions.Compiled);

    private StackPanel? _rootPanel;

    static MarkdownMessageView()
    {
        MarkdownTextProperty.Changed.AddClassHandler<MarkdownMessageView>((view, _) => view.RenderMarkdown());
    }

    public MarkdownMessageView()
    {
        InitializeComponent();
        _rootPanel = this.FindControl<StackPanel>("RootPanel");
        RenderMarkdown();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RenderMarkdown();
    }

    public string? MarkdownText
    {
        get => GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void RenderMarkdown()
    {
        if (_rootPanel is null)
        {
            return;
        }

        _rootPanel.Children.Clear();
        var markdown = MarkdownText;
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');

        var paragraphLines = new List<string>();
        var codeBlockLines = new List<string>();
        var inCodeBlock = false;
        string? codeLanguage = null;

        foreach (var line in lines)
        {
            if (IsFence(line, out var fenceLanguage))
            {
                FlushParagraph(paragraphLines);

                if (inCodeBlock)
                {
                    AddCodeBlock(string.Join('\n', codeBlockLines), codeLanguage);
                    codeBlockLines.Clear();
                    codeLanguage = null;
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                    codeLanguage = fenceLanguage;
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(paragraphLines);
                continue;
            }

            if (HeadingRegex.IsMatch(line)
                || UnorderedListRegex.IsMatch(line)
                || OrderedListRegex.IsMatch(line)
                || BlockQuoteRegex.IsMatch(line)
                || HorizontalRuleRegex.IsMatch(line))
            {
                FlushParagraph(paragraphLines);
                _ = TryAddHeading(line) || TryAddListItem(line) || TryAddBlockQuote(line) || TryAddHorizontalRule(line);
                continue;
            }

            paragraphLines.Add(line);
        }

        FlushParagraph(paragraphLines);
        if (inCodeBlock)
        {
            AddCodeBlock(string.Join('\n', codeBlockLines), codeLanguage);
        }
    }

    private void FlushParagraph(List<string> paragraphLines)
    {
        if (paragraphLines.Count == 0)
        {
            return;
        }

        AddParagraph(string.Join('\n', paragraphLines));
        paragraphLines.Clear();
    }

    private bool TryAddHeading(string line)
    {
        var match = HeadingRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var level = Math.Clamp(match.Groups[1].Value.Length, 1, 6);
        var heading = match.Groups[2].Value.Trim();

        var headingBlock = CreateTextBlock();
        headingBlock.FontWeight = FontWeight.Bold;
        headingBlock.FontSize = level switch
        {
            1 => 22,
            2 => 20,
            3 => 18,
            4 => 16,
            5 => 15,
            _ => 14
        };
        headingBlock.Margin = new Thickness(0, 4, 0, 0);
        AppendInlineContent(headingBlock.Inlines!, heading);
        _rootPanel?.Children.Add(headingBlock);
        return true;
    }

    private bool TryAddListItem(string line)
    {
        var unorderedMatch = UnorderedListRegex.Match(line);
        if (unorderedMatch.Success)
        {
            AddListItem("\u2022", unorderedMatch.Groups[1].Value.Trim());
            return true;
        }

        var orderedMatch = OrderedListRegex.Match(line);
        if (!orderedMatch.Success)
        {
            return false;
        }

        AddListItem($"{orderedMatch.Groups[1].Value}.", orderedMatch.Groups[2].Value.Trim());
        return true;
    }

    private void AddListItem(string marker, string content)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*")
        };

        var bullet = new TextBlock
        {
            Text = marker,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Top,
            TextWrapping = TextWrapping.NoWrap
        };
        bullet.Foreground = TryFindBrush("ChatAssistantLabelForegroundBrush") ?? GetDefaultTextBrush();

        var textBlock = CreateTextBlock();
        textBlock.Margin = new Thickness(8, 0, 0, 0);
        AppendInlineContent(textBlock.Inlines!, content);

        grid.Children.Add(bullet);
        Grid.SetColumn(bullet, 0);
        grid.Children.Add(textBlock);
        Grid.SetColumn(textBlock, 1);

        _rootPanel?.Children.Add(grid);
    }

    private bool TryAddBlockQuote(string line)
    {
        var match = BlockQuoteRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var quoteBorder = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2)
        };
        quoteBorder.BorderBrush = TryFindBrush("MarkdownQuoteBorderBrush");

        var quoteTextBlock = CreateTextBlock();
        quoteTextBlock.FontStyle = FontStyle.Italic;
        AppendInlineContent(quoteTextBlock.Inlines!, match.Groups[1].Value.Trim());

        quoteBorder.Child = quoteTextBlock;
        _rootPanel?.Children.Add(quoteBorder);
        return true;
    }

    private bool TryAddHorizontalRule(string line)
    {
        if (!HorizontalRuleRegex.IsMatch(line))
        {
            return false;
        }

        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 4)
        };
        separator.Background = TryFindBrush("MarkdownCodeBorderBrush");
        _rootPanel?.Children.Add(separator);
        return true;
    }

    private void AddParagraph(string text)
    {
        var paragraph = CreateTextBlock();
        AppendInlineContent(paragraph.Inlines!, text.TrimEnd());
        _rootPanel?.Children.Add(paragraph);
    }

    private void AddCodeBlock(string code, string? language)
    {
        var content = string.IsNullOrWhiteSpace(language)
            ? code
            : $"// {language}\n{code}";

        var codeTextBlock = new TextBlock
        {
            Text = content.TrimEnd(),
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas, Monaco, Courier New"),
            LineHeight = 22
        };
        codeTextBlock.Foreground = TryFindBrush("ChatAssistantTextForegroundBrush") ?? GetDefaultTextBrush();

        var codeScrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = codeTextBlock
        };

        var codeBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = codeScrollViewer
        };
        codeBorder.Background = TryFindBrush("MarkdownCodeBackgroundBrush");
        codeBorder.BorderBrush = TryFindBrush("MarkdownCodeBorderBrush");
        codeBorder.BorderThickness = new Thickness(1);

        _rootPanel?.Children.Add(codeBorder);
    }

    private TextBlock CreateTextBlock()
    {
        var textBlock = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22
        };
        textBlock.Foreground = TryFindBrush("ChatAssistantTextForegroundBrush") ?? GetDefaultTextBrush();
        return textBlock;
    }

    private static bool IsFence(string line, out string? language)
    {
        language = null;
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return false;
        }

        language = trimmed.Length > 3 ? trimmed[3..].Trim() : null;
        return true;
    }

    private static void AppendInlineContent(InlineCollection inlines, string text)
    {
        var cursor = 0;
        while (cursor < text.Length)
        {
            if (text[cursor] == '\n')
            {
                inlines.Add(new LineBreak());
                cursor++;
                continue;
            }

            if (TryReadInlineCode(text, cursor, out var codeEnd, out var codeText))
            {
                inlines.Add(new Run(codeText)
                {
                    FontFamily = new FontFamily("Consolas, Monaco, Courier New"),
                    FontWeight = FontWeight.SemiBold
                });
                cursor = codeEnd;
                continue;
            }

            if (TryReadStrong(text, cursor, out var strongEnd, out var strongText))
            {
                var span = new Span
                {
                    FontWeight = FontWeight.Bold
                };
                AppendInlineContent(span.Inlines!, strongText);
                inlines.Add(span);
                cursor = strongEnd;
                continue;
            }

            if (TryReadLink(text, cursor, out var linkEnd, out var linkText))
            {
                var span = new Span();
                AppendInlineContent(span.Inlines!, linkText);
                inlines.Add(span);
                cursor = linkEnd;
                continue;
            }

            var plainEnd = FindNextInlineToken(text, cursor);
            inlines.Add(new Run(text[cursor..plainEnd]));
            cursor = plainEnd;
        }
    }

    private static bool TryReadInlineCode(string text, int startIndex, out int nextIndex, out string codeText)
    {
        nextIndex = startIndex;
        codeText = string.Empty;

        if (text[startIndex] != '`')
        {
            return false;
        }

        var endIndex = text.IndexOf('`', startIndex + 1);
        if (endIndex <= startIndex + 1)
        {
            return false;
        }

        codeText = text[(startIndex + 1)..endIndex];
        nextIndex = endIndex + 1;
        return true;
    }

    private static bool TryReadStrong(string text, int startIndex, out int nextIndex, out string strongText)
    {
        nextIndex = startIndex;
        strongText = string.Empty;

        if (!HasMarker(text, startIndex, "**") && !HasMarker(text, startIndex, "__"))
        {
            return false;
        }

        var marker = text.Substring(startIndex, 2);
        var endIndex = text.IndexOf(marker, startIndex + 2, StringComparison.Ordinal);
        if (endIndex <= startIndex + 2)
        {
            return false;
        }

        strongText = text[(startIndex + 2)..endIndex];
        nextIndex = endIndex + 2;
        return true;
    }

    private static bool TryReadLink(string text, int startIndex, out int nextIndex, out string linkText)
    {
        nextIndex = startIndex;
        linkText = string.Empty;

        if (text[startIndex] != '[')
        {
            return false;
        }

        var textEnd = text.IndexOf(']', startIndex + 1);
        if (textEnd <= startIndex + 1 || textEnd + 1 >= text.Length || text[textEnd + 1] != '(')
        {
            return false;
        }

        var urlEnd = text.IndexOf(')', textEnd + 2);
        if (urlEnd <= textEnd + 2)
        {
            return false;
        }

        var label = text[(startIndex + 1)..textEnd];
        var url = text[(textEnd + 2)..urlEnd];
        linkText = string.Equals(label, url, StringComparison.Ordinal) ? label : $"{label} ({url})";
        nextIndex = urlEnd + 1;
        return true;
    }

    private static int FindNextInlineToken(string text, int startIndex)
    {
        var nextIndex = startIndex;
        while (nextIndex < text.Length)
        {
            if (text[nextIndex] is '\n' or '`' or '[')
            {
                break;
            }

            if (HasMarker(text, nextIndex, "**") || HasMarker(text, nextIndex, "__"))
            {
                break;
            }

            nextIndex++;
        }

        return nextIndex == startIndex ? startIndex + 1 : nextIndex;
    }

    private static bool HasMarker(string text, int index, string marker)
    {
        return index + marker.Length <= text.Length
            && string.Compare(text, index, marker, 0, marker.Length, StringComparison.Ordinal) == 0;
    }

    private IBrush? TryFindBrush(string resourceKey)
    {
        if (this.TryGetResource(resourceKey, ActualThemeVariant, out var localResource)
            && localResource is IBrush localBrush)
        {
            return localBrush;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null
            && topLevel.TryGetResource(resourceKey, ActualThemeVariant, out var topLevelResource)
            && topLevelResource is IBrush topLevelBrush)
        {
            return topLevelBrush;
        }

        if (Application.Current != null
            && Application.Current.TryGetResource(resourceKey, ActualThemeVariant, out var appResource)
            && appResource is IBrush appBrush)
        {
            return appBrush;
        }

        return null;
    }

    private IBrush GetDefaultTextBrush()
    {
        return ActualThemeVariant == ThemeVariant.Dark
            ? Brushes.White
            : Brushes.Black;
    }
}
