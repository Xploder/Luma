using static Luma.UiText;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Input;
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
    private readonly (string Name, byte Code)[] modes = { (T("Dauerhaft"), 1), (T("Sanft pulsieren"), 8), (T("Schnell pulsieren"), 9), (T("Langsam blinken"), 3), (T("Schnell blinken"), 2), (T("Ladeanimation"), 4), (T("Einblenden"), 13) };

    public MainWindow()
    {
        settings = demo ? new() : LedSettings.Load();
        InitializeComponent();
        ExtendsContentIntoTitleBar = true; SetTitleBar(TitleBar);
        // AppWindow sizes use physical pixels; keep the initial layout compact
        // in device-independent pixels on scaled Windows desktops as well.
        Root.Loaded += (_, _) =>
        {
            UiLocalization.Apply(Root);
            double scale = Root.XamlRoot.RasterizationScale;
            AppWindow.Resize(new SizeInt32((int)(480 * scale), (int)(610 * scale)));
            if (AppWindow.Presenter is OverlappedPresenter sizedPresenter)
                sizedPresenter.PreferredMinimumWidth = (int)(380 * scale);
        };
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "Luma.ico"));
        if (AppWindow.Presenter is OverlappedPresenter presenter) presenter.PreferredMinimumWidth = 380;
        ContentPanel.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(ContentWheel), true);
        Root.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(WindowWheel), true);
        Brightness.ForwardWheel = ScrollWheel;
        Modes.ForwardWheel = ScrollWheel;
        Controllers.ForwardWheel = ScrollWheel;
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
        foreach (var mode in modes) Modes.Items.Add(mode.Name);
        Modes.SelectedIndex = Array.FindIndex(modes, x => x.Code == settings.Mode);
        Brightness.Value = settings.Brightness;
        LedEnabled.IsOn = settings.Enabled;
        AutoApply.IsOn = settings.AutoApply;
        CloseToTray.IsOn = settings.CloseToTray;
        Startup.IsOn = !demo && LedSettings.IsStartupEnabled();
        Startup.IsEnabled = !demo;
        tray = new NativeTray(WinRT.Interop.WindowNative.GetWindowHandle(this), ShowWindow, () => LedEnabled.IsOn = !LedEnabled.IsOn, SetPreset, Quit);
        TrayButton.IsEnabled = tray.IsAvailable;
        if (!tray.IsAvailable) ShowNotice(T("Tray-Symbol nicht verfügbar"), T("Windows hat das Tray-Symbol nicht angenommen. Luma bleibt als Fenster geöffnet."));
        AppWindow.Closing += (_, e) => { if (!exiting && settings.CloseToTray && tray.IsAvailable) { e.Cancel = true; HideToTray(); } };
        Closed += (_, _) => Shutdown();
        debounce.Tick += async (_, _) => { debounce.Stop(); await SendAsync(); };
        reconnect.Tick += async (_, _) => await RefreshAsync(false);
        initializing = false;
        UpdateControls();
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
                Controllers.Items.Clear();
                for (int i = 0; i < devices.Count; i++)
                    Controllers.Items.Add($"Xbox Controller {i + 1} · {devices[i]:X4}");
                Controllers.SelectedIndex = devices.Count == 0 ? -1 : Math.Max(0, devices.IndexOf(old));
                selected = Controllers.SelectedIndex >= 0 ? devices[Controllers.SelectedIndex] : 0;
            }
            Controllers.PlaceholderText = devices.Count == 0 ? T("Kein Controller verbunden") : T("Controller auswählen");
            ConnectionText.Text = demo ? T("Vorschaumodus · keine Hardwarebefehle") : devices.Count > 0 ? T("Verbunden über XboxGIP · USB / Xbox Wireless") : T("USB oder Xbox Wireless Adapter verbinden · Bluetooth unterstützt keine LED-Steuerung");
            if (changed || manual) SetStatus(demo ? T("Vorschaumodus · es wird kein Controller gesteuert") : devices.Count > 0 ? T("Bereit · Änderungen werden direkt angewendet") : T("Warte auf einen Controller …"), devices.Count > 0);
            if (devices.Count > 0 && settings.AutoApply && newIds.Length > 0)
            {
                bool enabled = settings.Enabled; byte mode = settings.Mode; int brightness = settings.Brightness;
                await ioGate.WaitAsync();
                try { if (!demo) await Task.Run(() => { foreach (var id in newIds) backend.SetLed(id, enabled, mode, brightness); }); }
                finally { ioGate.Release(); }
                if (!exiting) SetStatus(demo ? T("Vorschaumodus · es wird kein Controller gesteuert") : T("Gespeicherte Einstellung angewendet"), true);
            }
        }
        catch (Exception ex)
        {
            if (exiting) return;
            await ioGate.WaitAsync(); try { backend.Dispose(); } finally { ioGate.Release(); }
            devices.Clear(); selected = 0; Controllers.Items.Clear();
            Controllers.PlaceholderText = T("Kein Controller verbunden");
            ConnectionText.Text = T("USB oder Xbox Wireless Adapter verbinden. Bluetooth wird nicht unterstützt.");
            SetStatus(T("Warte auf einen Controller …"), false);
            if (manual) ShowNotice(T("Controller nicht erreichbar"), ex.Message);
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
        revision++; Save(); UpdateControls(); debounce.Stop(); debounce.Start();
    }
    private void UpdateControls()
    {
        BrightnessValue.Text = $"{settings.Brightness} %";
        Brightness.IsEnabled = settings.Enabled;
        Modes.IsEnabled = settings.Enabled;
    }
    private void ContentWheel(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Handled) ScrollWheel(e);
    }
    private void WindowWheel(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Handled) ScrollWheel(e);
    }
    private void ScrollWheel(PointerRoutedEventArgs e)
    {
        // A popup is a separate scrolling surface; keep its list usable.
        if (Modes.IsDropDownOpen || Controllers.IsDropDownOpen) return;
        var point = e.GetCurrentPoint(Root);
        if (point.Properties.IsHorizontalMouseWheel) return;
        double offset = MainScroll.VerticalOffset - point.Properties.MouseWheelDelta / 120.0 * 48;
        MainScroll.ChangeView(null, Math.Clamp(offset, 0, MainScroll.ScrollableHeight), null, true);
        e.Handled = true;
    }
    private async Task SendAsync()
    {
        if (exiting) return;
        if (pendingTarget == 0) { SetStatus(settings.AutoApply ? T("Gespeichert · wird beim nächsten Verbinden angewendet") : T("Gespeichert · automatisches Anwenden ist deaktiviert"), false); return; }
        int currentRevision = revision;
        ulong target = pendingTarget; int brightness = settings.Brightness; byte mode = settings.Mode; bool enabled = settings.Enabled;
        await ioGate.WaitAsync();
        try
        {
            if (exiting || currentRevision != revision) return;
            if (!demo) await Task.Run(() => backend.SetLed(target, enabled, mode, brightness));
            if (exiting || currentRevision != revision) return;
            Notice.IsOpen = false;
            SetStatus(demo ? T("Vorschaumodus · es wird kein Controller gesteuert") : enabled && brightness > 0 ? string.Format(T("Helligkeit {0} % · angewendet"), brightness) : T("LED ausgeschaltet"), true);
        }
        catch (Exception ex) { if (!exiting) { SetStatus(T("Änderung konnte nicht angewendet werden"), false); ShowNotice(T("Verbindung prüfen"), ex.Message); } }
        finally { ioGate.Release(); }
    }
    private void AutoApply_Toggled(object sender, RoutedEventArgs e) { if (!initializing) { settings.AutoApply = AutoApply.IsOn; Save(); } }
    private void CloseToTray_Toggled(object sender, RoutedEventArgs e) { if (!initializing) { settings.CloseToTray = CloseToTray.IsOn; Save(); } }
    private void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (initializing || demo) return;
        try { LedSettings.SetStartup(Startup.IsOn); }
        catch (Exception ex) { initializing = true; Startup.IsOn = LedSettings.IsStartupEnabled(); initializing = false; ShowNotice(T("Autostart nicht geändert"), ex.Message); }
    }
    private void Save() { if (!demo) { try { settings.Save(); } catch (Exception ex) { ShowNotice(T("Einstellung nicht gespeichert"), ex.Message); } } }
    private void ShowNotice(string title, string message) { Notice.Title = title; Notice.Message = message; Notice.IsOpen = true; }
    private void SetStatus(string message, bool success) { Status.Text = message; StatusDot.Fill = new SolidColorBrush(success ? Windows.UI.Color.FromArgb(255, 201, 242, 123) : Windows.UI.Color.FromArgb(255, 120, 132, 143)); }
}
