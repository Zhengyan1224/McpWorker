using System.Diagnostics;
using System.Text;

namespace Zhengyan.ChatUI.CLI.Services;

public static class ClipboardService
{
    public static bool TrySetText(string text, out string? error)
    {
        if (string.IsNullOrEmpty(text))
        {
            error = "Nothing to copy.";
            return false;
        }

        var candidates = GetClipboardCommands();
        foreach (var candidate in candidates)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = candidate.FileName,
                        Arguments = candidate.Arguments,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                process.StandardInput.Write(text);
                process.StandardInput.Close();
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    error = null;
                    return true;
                }
            }
            catch
            {
            }
        }

        error = "Clipboard integration is unavailable on this machine.";
        return false;
    }

    private static IEnumerable<ClipboardCommand> GetClipboardCommands()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return new ClipboardCommand("clip", string.Empty);
            yield break;
        }

        if (OperatingSystem.IsMacOS())
        {
            yield return new ClipboardCommand("pbcopy", string.Empty);
            yield break;
        }

        if (OperatingSystem.IsLinux())
        {
            yield return new ClipboardCommand("wl-copy", string.Empty);
            yield return new ClipboardCommand("xclip", "-selection clipboard");
            yield return new ClipboardCommand("xsel", "--clipboard --input");
        }
    }

    private readonly record struct ClipboardCommand(string FileName, string Arguments);
}
