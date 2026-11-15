using static Luma.UiText;
using System.Runtime.InteropServices;

namespace Luma;

public sealed class NativeTray : IDisposable
{
    private const uint Callback = 0x8001;
    private readonly IntPtr hwnd;
    private readonly SubclassProc proc;
    private readonly Action show, toggle, quit;
    private readonly Action<int> preset;
    private bool added;
    private readonly IntPtr icon;
    private readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    public NativeTray(IntPtr window, Action show, Action toggle, Action<int> preset, Action quit)
    {
        hwnd = window; this.show = show; this.toggle = toggle; this.preset = preset; this.quit = quit;
        icon = LoadImage(IntPtr.Zero, Path.Combine(AppContext.BaseDirectory, "Assets", "Luma.ico"), 1, 32, 32, 0x10);
        proc = WndProc;
        if (!SetWindowSubclass(hwnd, proc, 1, UIntPtr.Zero)) throw new InvalidOperationException(T("Tray-Fenster konnte nicht eingerichtet werden."));
        Add();
    }
    private NotifyData Data() => new() { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = hwnd, Id = 1, Flags = 7 | 0x80, Callback = Callback, Icon = icon != IntPtr.Zero ? icon : LoadIcon(IntPtr.Zero, (IntPtr)32512), Tip = "Luma · Controller LED", Info = "", InfoTitle = "" };
    private void Add() { var data = Data(); added = Shell_NotifyIcon(0, ref data); if (added) { data.Version = 4; Shell_NotifyIcon(4, ref data); } }
    public bool IsAvailable => added;
    private IntPtr WndProc(IntPtr h, uint message, UIntPtr w, IntPtr l, UIntPtr id, UIntPtr data)
    {
        if (message == taskbarCreated) { Add(); return IntPtr.Zero; }
        if (message == 0x8002) { show(); return IntPtr.Zero; }
        if (message == Callback)
        {
            uint notification = (uint)(l.ToInt64() & 0xFFFF);
            if (notification == 0x0203 || notification == 0x0400 || notification == 0x0401) show();
            else if (notification == 0x0205 || notification == 0x007B)
            {
                IntPtr menu = CreatePopupMenu();
                try
                {
                    AppendMenu(menu, 0, 1, T("Luma öffnen"));
                    AppendMenu(menu, 0, 2, T("LED ein / aus"));
                    AppendMenu(menu, 0x800, 0, "");
                    AppendMenu(menu, 0, 3, T("Nacht · 10 %"));
                    AppendMenu(menu, 0, 4, T("Entspannt · 30 %"));
                    AppendMenu(menu, 0, 5, T("Hell · 100 %"));
                    AppendMenu(menu, 0x800, 0, "");
                    AppendMenu(menu, 0, 6, T("Beenden"));
                    GetCursorPos(out var point); SetForegroundWindow(hwnd);
                    uint choice = TrackPopupMenu(menu, 0x102, point.X, point.Y, 0, hwnd, IntPtr.Zero);
                    PostMessage(hwnd, 0, UIntPtr.Zero, IntPtr.Zero);
                    switch (choice) { case 1: show(); break; case 2: toggle(); break; case 3: preset(10); break; case 4: preset(30); break; case 5: preset(100); break; case 6: quit(); break; }
                }
                finally { DestroyMenu(menu); }
            }
            return IntPtr.Zero;
        }
        return DefSubclassProc(h, message, w, l);
    }
    public static void ShowExisting()
    {
        var window = FindWindow(null, "Luma · Controller LED");
        if (window != IntPtr.Zero) PostMessage(window, 0x8002, UIntPtr.Zero, IntPtr.Zero);
    }
    public void Hide() { if (added) ShowWindow(hwnd, 0); }
    public void Show() { ShowWindow(hwnd, 9); SetForegroundWindow(hwnd); }
    public void Dispose() { var data = Data(); if (added) Shell_NotifyIcon(2, ref data); RemoveWindowSubclass(hwnd, proc, 1); if (icon != IntPtr.Zero) DestroyIcon(icon); added = false; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct NotifyData
    {
        public uint Size; public IntPtr Window; public uint Id; public uint Flags; public uint Callback; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State; public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public IntPtr BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    private delegate IntPtr SubclassProc(IntPtr h, uint m, UIntPtr w, IntPtr l, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr h, SubclassProc proc, uint id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr h, SubclassProc proc, uint id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr h, uint m, UIntPtr w, IntPtr l);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint action, ref NotifyData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string text);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr instance, IntPtr id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr menu, uint flags, uint id, string text);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rect);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
}
