using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Luma;

public sealed partial class MainWindow : Window
{
    private readonly bool demo = Environment.GetCommandLineArgs().Contains("--demo");
    private readonly LedSettings settings;
    private readonly GipController backend = new();
    private readonly SemaphoreSlim ioGate = new(1, 1);
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly DispatcherTimer reconnect = new() { Interval = TimeSpan.FromSeconds(8) };
    private NativeTray? tray;
    private bool initializing = true, refreshing, exiting;
    private List<ulong> devices = new();
    private ulong selected;
    private ulong pendingTarget;
    private int revision;
    private readonly (string Name, byte Code)[] modes = { ("Dauerhaft", 1), ("Sanft pulsieren", 8), ("Schnell pulsieren", 9), ("Langsam blinken", 3), ("Schnell blinken", 2), ("Ladeanimation", 4), ("Einblenden", 13) };

    public MainWindow()
    {
        settings = demo ? new() : LedSettings.Load();
        InitializeComponent();
        ExtendsContentIntoTitleBar = true; SetTitleBar(TitleBar);
        AppWindow.Resize(new SizeInt32(1060, 900));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Luma.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.PreferredMinimumWidth = 900;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
        Modes.ItemsSource = modes.Select(x => x.Name).ToArray();
        Modes.SelectedIndex = Array.FindIndex(modes, x => x.Code == settings.Mode);
        Brightness.Value = settings.Brightness;
        LedEnabled.IsOn = settings.Enabled;
        AutoApply.IsOn = settings.AutoApply;
        CloseToTray.IsOn = settings.CloseToTray;
        Startup.IsOn = !demo && LedSettings.IsStartupEnabled();
        Startup.IsEnabled = !demo;
        tray = new NativeTray(WinRT.Interop.WindowNative.GetWindowHandle(this), ShowWindow, () => LedEnabled.IsOn = !LedEnabled.IsOn, SetPreset, Quit);
        TrayButton.IsEnabled = tray.IsAvailable;
        if (!tray.IsAvailable) ShowNotice("Tray-Symbol nicht verfügbar", "Windows hat das Tray-Symbol nicht angenommen. Luma bleibt als Fenster geöffnet.");
        AppWindow.Closing += (_, e) => { if (!exiting && settings.CloseToTray && tray.IsAvailable) { e.Cancel = true; HideToTray(); } };
        Closed += (_, _) => Shutdown();
        debounce.Tick += async (_, _) => { debounce.Stop(); await SendAsync(); };
        reconnect.Tick += async (_, _) => await RefreshAsync(false);
        initializing = false;
        UpdatePreview();
        DispatcherQueue.TryEnqueue(async () => { await RefreshAsync(true); if (!exiting) reconnect.Start(); });
    }
    public void HideToTray() => tray?.Hide();
    private void ShowWindow() { tray?.Show(); Activate(); }
    private void Quit() { exiting = true; Close(); }
    private void Shutdown()
    {
        if (exiting && tray == null) return;
        exiting = true; reconnect.Stop(); debounce.Stop(); tray?.Dispose(); tray = null;
        // Never dispose an active native I/O handle on the UI thread.
        _ = Task.Run(async () => { await ioGate.WaitAsync(); try { backend.Dispose(); } finally { ioGate.Release(); } });
    }
    private void Tray_Click(object sender, RoutedEventArgs e) => HideToTray();
    private void Quit_Click(object sender, RoutedEventArgs e) => Quit();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync(true);
    private void Controller_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (Controllers.SelectedIndex >= 0 && Controllers.SelectedIndex < devices.Count) selected = devices[Controllers.SelectedIndex];
    }
    private async Task RefreshAsync(bool manual)
    {
        if (refreshing || exiting) return;
        refreshing = true; RefreshButton.IsEnabled = false;
        try
        {
            List<ulong> found;
            await ioGate.WaitAsync();
            try { found = demo ? new() { 0x1914 } : (await Task.Run(backend.Discover)).ToList(); }
            finally { ioGate.Release(); }
            if (exiting) return;
            var newIds = found.Except(devices).ToArray();
            bool changed = !found.SequenceEqual(devices);
            devices = found;
            if (changed || manual)
            {
                var old = selected;
                Controllers.ItemsSource = devices.Select((id, i) => $"Xbox Controller {i + 1} · {id:X4}").ToArray();
                Controllers.SelectedIndex = devices.Count == 0 ? -1 : Math.Max(0, devices.IndexOf(old));
                selected = Controllers.SelectedIndex >= 0 ? devices[Controllers.SelectedIndex] : 0;
            }
            Controllers.PlaceholderText = devices.Count == 0 ? "Kein Controller verbunden" : "Controller auswählen";
            ConnectionText.Text = demo ? "Vorschaumodus · keine Hardwarebefehle" : devices.Count > 0 ? "Verbunden über XboxGIP · USB / Xbox Wireless" : "USB oder Xbox Wireless Adapter verbinden · Bluetooth unterstützt keine LED-Steuerung";
            if (changed || manual) SetStatus(demo ? "Vorschaumodus · es wird kein Controller gesteuert" : devices.Count > 0 ? "Bereit · Änderungen werden direkt angewendet" : "Warte auf einen Controller …", devices.Count > 0);
            if (devices.Count > 0 && settings.AutoApply && newIds.Length > 0)
            {
                bool enabled = settings.Enabled; byte mode = settings.Mode; int brightness = settings.Brightness;
                await ioGate.WaitAsync();
                try { if (!demo) await Task.Run(() => { foreach (var id in newIds) backend.SetLed(id, enabled, mode, brightness); }); }
                finally { ioGate.Release(); }
                if (!exiting) SetStatus(demo ? "Vorschaumodus · es wird kein Controller gesteuert" : "Gespeicherte Einstellung angewendet", true);
            }
        }
        catch (Exception ex)
        {
            if (exiting) return;
            await ioGate.WaitAsync(); try { backend.Dispose(); } finally { ioGate.Release(); }
            devices.Clear(); selected = 0; Controllers.ItemsSource = Array.Empty<string>();
            Controllers.PlaceholderText = "Kein Controller verbunden";
            ConnectionText.Text = "USB oder Xbox Wireless Adapter verbinden. Bluetooth wird nicht unterstützt.";
            SetStatus("Warte auf einen Controller …", false);
            if (manual) ShowNotice("Controller nicht erreichbar", ex.Message);
        }
        finally { refreshing = false; if (!exiting) RefreshButton.IsEnabled = true; }
    }
    private void Brightness_Changed(object sender, RangeBaseValueChangedEventArgs e) { if (!initializing) Changed(); }
    private void Led_Toggled(object sender, RoutedEventArgs e) { if (!initializing) Changed(); }
    private void Mode_Changed(object sender, SelectionChangedEventArgs e) { if (!initializing) Changed(); }
    private void Preset_Click(object sender, RoutedEventArgs e) => SetPreset(int.Parse(((Button)sender).Tag.ToString()!));
    private void SetPreset(int value)
    {
        initializing = true; Brightness.Value = value; LedEnabled.IsOn = true; Modes.SelectedIndex = 0; initializing = false; Changed();
    }
    private void Changed()
    {
        settings.Brightness = (int)Math.Round(Brightness.Value); settings.Enabled = LedEnabled.IsOn;
        settings.Mode = modes[Math.Max(0, Modes.SelectedIndex)].Code;
        pendingTarget = selected;
        revision++; Save(); UpdatePreview(); debounce.Stop(); debounce.Start();
    }
    private void UpdatePreview()
    {
        BrightnessValue.Text = settings.Brightness.ToString();
        double strength = settings.Enabled ? settings.Brightness / 100.0 : 0;
        LedDot.Opacity = 0.12 + strength * .88; LedGlow.Opacity = strength;
        PreviewTitle.Text = strength == 0 ? "Licht aus. Fokus an." : strength <= .15 ? "Bereit für die Nacht" : strength <= .4 ? "Angenehm dezent" : "Deutlich sichtbar";
        PreviewBadge.Text = settings.Enabled && settings.Brightness > 0 ? "LED-VORSCHAU" : "LED AUS";
        Brightness.IsEnabled = settings.Enabled;
        Modes.IsEnabled = settings.Enabled;
    }
    private async Task SendAsync()
    {
        if (exiting) return;
        if (pendingTarget == 0) { SetStatus(settings.AutoApply ? "Gespeichert · wird beim nächsten Verbinden angewendet" : "Gespeichert · automatisches Anwenden ist deaktiviert", false); return; }
        int currentRevision = revision;
        ulong target = pendingTarget; int brightness = settings.Brightness; byte mode = settings.Mode; bool enabled = settings.Enabled;
        await ioGate.WaitAsync();
        try
        {
            if (exiting || currentRevision != revision) return;
            if (!demo) await Task.Run(() => backend.SetLed(target, enabled, mode, brightness));
            if (exiting || currentRevision != revision) return;
            Notice.IsOpen = false;
            SetStatus(demo ? "Vorschaumodus · es wird kein Controller gesteuert" : enabled && brightness > 0 ? $"Helligkeit {brightness} % · angewendet" : "LED ausgeschaltet", true);
        }
        catch (Exception ex) { if (!exiting) { SetStatus("Änderung konnte nicht angewendet werden", false); ShowNotice("Verbindung prüfen", ex.Message); } }
        finally { ioGate.Release(); }
    }
    private void AutoApply_Toggled(object sender, RoutedEventArgs e) { if (!initializing) { settings.AutoApply = AutoApply.IsOn; Save(); } }
    private void CloseToTray_Toggled(object sender, RoutedEventArgs e) { if (!initializing) { settings.CloseToTray = CloseToTray.IsOn; Save(); } }
    private void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (initializing || demo) return;
        try { LedSettings.SetStartup(Startup.IsOn); }
        catch (Exception ex) { initializing = true; Startup.IsOn = LedSettings.IsStartupEnabled(); initializing = false; ShowNotice("Autostart nicht geändert", ex.Message); }
    }
    private void Save() { if (!demo) { try { settings.Save(); } catch (Exception ex) { ShowNotice("Einstellung nicht gespeichert", ex.Message); } } }
    private void ShowNotice(string title, string message) { Notice.Title = title; Notice.Message = message; Notice.IsOpen = true; }
    private void SetStatus(string message, bool success) { Status.Text = message; StatusDot.Fill = new SolidColorBrush(success ? Windows.UI.Color.FromArgb(255, 201, 242, 123) : Windows.UI.Color.FromArgb(255, 120, 132, 143)); }
}
