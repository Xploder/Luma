using Microsoft.UI.Xaml;

namespace Luma;

public partial class App : Application
{
    private MainWindow? window;
    private Mutex? instance;
    public App()
    {
        UnhandledException += (_, e) => LogStartupError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => LogStartupError(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        try { InitializeComponent(); } catch (Exception e) { LogStartupError(e); throw; }
    }
    internal static void LogStartupError(Exception e)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "Luma-error.log"), $"{DateTime.Now:O}\n{e}\n\n"); } catch { }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        instance = new Mutex(true, "Local\\Luma.XboxLed.WinUI", out bool first);
        if (!first) { NativeTray.ShowExisting(); Exit(); return; }
        try
        {
            window = new MainWindow();
            window.Activate();
            if (Environment.GetCommandLineArgs().Contains("--tray")) window.HideToTray();
        }
        catch (Exception e) { LogStartupError(e); Exit(); }
    }
}
