using System.Globalization;

namespace Zhengyan.ChatUI.CLI.Models;

public sealed class CliLaunchOptions
{
    public bool ShowHelp { get; private set; }

    public bool ShowConfigPath { get; private set; }

    public bool SaveSettings { get; private set; }

    public bool ReadMessageFromStdIn { get; private set; }

    public string? OutputPath { get; private set; }

    public string? Message { get; private set; }

    public string? ServerEndpoint { get; private set; }

    public string? ApiKey { get; private set; }

    public string? Model { get; private set; }

    public int? MaxTokens { get; private set; }

    public float? Temperature { get; private set; }

    public float? TopP { get; private set; }

    public bool? UseResponsesApi { get; private set; }

    public List<string> ImageFiles { get; } = [];

    public List<string> ImageUrls { get; } = [];

    public bool IsNonInteractive =>
        !string.IsNullOrWhiteSpace(Message)
        || ReadMessageFromStdIn
        || ImageFiles.Count > 0
        || ImageUrls.Count > 0;

    public static CliLaunchOptions Parse(string[] args)
    {
        var options = new CliLaunchOptions();
        var positionalTokens = new List<string>();

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            switch (arg.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                    options.ShowHelp = true;
                    break;
                case "--config-path":
                    options.ShowConfigPath = true;
                    break;
                case "--save":
                    options.SaveSettings = true;
                    break;
                case "--stdin":
                    options.ReadMessageFromStdIn = true;
                    break;
                case "--output":
                    options.OutputPath = ReadRequiredValue(args, ref index, arg);
                    break;
                case "--message":
                case "-m":
                    options.Message = ReadRequiredValue(args, ref index, arg);
                    break;
                case "--server":
                    options.ServerEndpoint = ReadRequiredValue(args, ref index, arg);
                    break;
                case "--token":
                case "--api-key":
                    options.ApiKey = ReadRequiredValue(args, ref index, arg);
                    break;
                case "--model":
                    options.Model = ReadRequiredValue(args, ref index, arg);
                    break;
                case "--max-tokens":
                    options.MaxTokens = ParseInt(ReadRequiredValue(args, ref index, arg), "max-tokens");
                    break;
                case "--temperature":
                    options.Temperature = ParseFloat(ReadRequiredValue(args, ref index, arg), "temperature");
                    break;
                case "--top-p":
                    options.TopP = ParseFloat(ReadRequiredValue(args, ref index, arg), "top-p");
                    break;
                case "--api":
                    options.UseResponsesApi = ParseApiMode(ReadRequiredValue(args, ref index, arg));
                    break;
                case "--file":
                case "--image-file":
                    options.ImageFiles.Add(ReadRequiredValue(args, ref index, arg));
                    break;
                case "--image-url":
                    options.ImageUrls.Add(ReadRequiredValue(args, ref index, arg));
                    break;
                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"Unknown option: {arg}");
                    }

                    positionalTokens.Add(arg);
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(options.Message) && positionalTokens.Count > 0)
        {
            options.Message = string.Join(' ', positionalTokens);
        }

        return options;
    }

    public void ApplyStandardInputMessage(string? standardInputText, bool append)
    {
        var normalizedText = standardInputText?.TrimEnd();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return;
        }

        if (append && !string.IsNullOrWhiteSpace(Message))
        {
            Message = $"{Message}{Environment.NewLine}{Environment.NewLine}{normalizedText}";
            return;
        }

        if (string.IsNullOrWhiteSpace(Message))
        {
            Message = normalizedText;
        }
    }

    private static string ReadRequiredValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"{optionName} requires a value.");
        }

        index++;
        return args[index];
    }

    private static int ParseInt(string rawValue, string optionName)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"{optionName} must be an integer.");
        }

        return value;
    }

    private static float ParseFloat(string rawValue, string optionName)
    {
        if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"{optionName} must be a valid number.");
        }

        return value;
    }

    private static bool ParseApiMode(string rawValue)
    {
        return rawValue.Trim().ToLowerInvariant() switch
        {
            "responses" or "response" => true,
            "chat" or "completions" => false,
            _ => throw new InvalidOperationException("api must be either 'chat' or 'responses'.")
        };
    }
}
