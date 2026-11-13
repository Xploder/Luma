using System.Text.Json;
using Microsoft.Win32;

namespace Luma;

public sealed class LedSettings
{
    public int Brightness { get; set; } = 20;
    public bool Enabled { get; set; } = true;
    public byte Mode { get; set; } = 1;
    public bool AutoApply { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public string Theme { get; set; } = "Dark";
    // A single default intentionally survives changing XboxGIP device IDs.
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumaXboxLed");
    public static string FileName => Path.Combine(Folder, "settings.json");
    public static LedSettings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<LedSettings>(File.ReadAllText(FileName)) ?? new();
            s.Brightness = Math.Clamp(s.Brightness, 0, 100);
            if (!new byte[] { 1, 2, 3, 4, 8, 9, 13 }.Contains(s.Mode)) s.Mode = 1;
            return s;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        string temp = FileName + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, FileName, true);
    }
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("LumaXboxLed") is string;
    }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("LumaXboxLed", $"\"{Environment.ProcessPath}\" --tray");
        else key.DeleteValue("LumaXboxLed", false);
    }
}
