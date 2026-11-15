using Luma;
using System.Buffers.Binary;

int checks = 0;
void Check(bool result, string message) { if (!result) throw new Exception(message); checks++; }
void Reject(Action action) { try { action(); } catch (ArgumentOutOfRangeException) { checks++; return; } throw new Exception("Invalid input accepted"); }
// Independent golden wire fixture: ID 0x0102030405060708, seq 7, steady, 100%.
var expected = new byte[] { 8,7,6,5,4,3,2,1, 10,32,7,0, 3,0,0,0, 0,0,0,0, 0,1,47 };
Check(LedPacket.Create(0x0102030405060708, 7, 1, 100).SequenceEqual(expected), "Wire format differs from the XboxGIP fixture");
Check(LedPacket.Create(1, 1, 0, 100)[22] == 0, "Off must set zero intensity");
Check(LedPacket.Create(1, 1, 1, 0)[21] == 0, "0% must turn off, not send steady at zero");
Check(LedPacket.Create(1, 1, 1, 10)[22] == 5, "Night preset conversion");
Check(LedPacket.Create(1, 1, 1, 30)[22] == 14, "Relaxed preset conversion");
foreach (byte mode in new byte[] { 0, 1, 2, 3, 4, 8, 9, 13 })
{
 int previous = -1;
 for (int value = 0; value <= 100; value++)
 {
  var p = LedPacket.Create(0xFFFFFFFFFFFFFFFF, 255, mode, value);
  Check(p.Length == 23 && p[22] <= 47 && p[22] >= previous, "Intensity must be bounded and monotonic");
  Check(BinaryPrimitives.ReadUInt64LittleEndian(p) == ulong.MaxValue && p[10] == 255, "Device ID and sequence preservation");
  previous = p[22];
 }
}
Reject(() => LedPacket.Create(0, 1, 1, 20));
Reject(() => LedPacket.Create(1, 0, 1, 20));
Reject(() => LedPacket.Create(1, 1, 7, 20));
Reject(() => LedPacket.Create(1, 1, 1, -1));
Reject(() => LedPacket.Create(1, 1, 1, 101));
var saved = new LedSettings { Brightness = 13, Enabled = false, Mode = 8, AutoApply = false, CloseToTray = true };
var json = System.Text.Json.JsonSerializer.Serialize(saved, SettingsJsonContext.Default.LedSettings);
var restored = System.Text.Json.JsonSerializer.Deserialize(json, SettingsJsonContext.Default.LedSettings)!;
Check(restored.Brightness == 13 && !restored.Enabled && restored.Mode == 8 && !restored.AutoApply && restored.CloseToTray, "Source-generated settings must preserve all values");
Console.WriteLine($"PASS: {checks} protocol/settings checks");
var previousCulture = System.Globalization.CultureInfo.CurrentUICulture;
try
{
 foreach (var culture in new[] { "de-DE", "de-AT", "en-US", "en-GB", "fr-FR" })
 {
  System.Globalization.CultureInfo.CurrentUICulture = new(culture);
  bool german = culture.StartsWith("de");
  Check(UiText.T("Helligkeit") == (german ? "Helligkeit" : "Brightness"), $"UI culture fallback: {culture}");
  Check(UiText.T("Luma öffnen") == (german ? "Luma öffnen" : "Open Luma"), $"Tray localization: {culture}");
 }
}
finally { System.Globalization.CultureInfo.CurrentUICulture = previousCulture; }
Console.WriteLine("PASS: German, English, and fallback localization");
var wheel = new WheelScrollState();
Check(wheel.TryAdvance(1_000, -120, 0, 200, out var offset) && offset == 48, "First wheel packet");
Check(!wheel.TryAdvance(1_000, -120, 0, 200, out _), "A routed packet must not scroll twice");
Check(wheel.TryAdvance(2_000, -120, 0, 200, out offset) && offset == 96, "Pending deltas must accumulate before the UI catches up");
Check(wheel.TryAdvance(3_000, 120, 0, 200, out offset) && offset == 48, "Direction reversal within a burst");
Check(wheel.TryAdvance(500_000, -120, 120, 200, out offset) && offset == 168, "Resynchronize after other scrolling");
Check(wheel.TryAdvance(501_000, -1200, 120, 200, out offset) && offset == 200, "Bottom boundary");
Check(wheel.TryAdvance(502_000, 1200, 120, 200, out offset) && offset == 0, "Top boundary");
Check(wheel.TryAdvance(503_000, -15, 0, 200, out offset) && offset == 6, "High-resolution wheel delta");
Check(wheel.TryAdvance(900_000, -120, 0, 0, out offset) && offset == 0, "Content that fits the window");
Console.WriteLine("PASS: wheel packet deduplication, queued deltas, direction changes, boundaries and high-resolution input");
if (args.Contains("--discover"))
{
 using var backend = new GipController();
 try { var ids = backend.Discover(); Console.WriteLine($"Read-only discovery: {ids.Count} controller(s)"); foreach (var id in ids) Console.WriteLine($"  {id:X16}"); }
 catch (Exception e) { Console.WriteLine($"Hardware discovery unavailable: {e.Message}"); }
}
