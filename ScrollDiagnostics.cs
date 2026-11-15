namespace Luma;

// Opt-in, local-only diagnostics for scroll issues. No other app's
// input, pointer positions, controller IDs, or user settings are recorded.
internal static class ScrollDiagnostics
{
    private static readonly object gate = new();
    internal static bool IsEnabled { get; } = Environment.GetCommandLineArgs().Contains("--scroll-diagnostics");
    private static readonly string path = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath)!, "Luma-scroll.log");
    internal static void Write(string message)
    {
        if (!IsEnabled) return;
        try
        {
            lock (gate)
            {
                if (File.Exists(path) && new FileInfo(path).Length > 2_000_000)
                    File.WriteAllText(path, "Log restarted at size limit.\n");
                File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {message}\n");
            }
        }
        catch { /* Diagnostics must never interrupt input. */ }
    }
}
