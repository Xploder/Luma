using System.Runtime.InteropServices;
using System.Text;

namespace Luma;

// Subclass only Luma's own HWNDs on its UI thread. Unlike WH_GETMESSAGE,
// this also receives wheel messages delivered synchronously with SendMessage.
internal sealed class NativeWheelInput : IDisposable
{
    private const uint SubclassId = 0x4c554d41;
    private readonly SubclassProc callback;
    private readonly IntPtr window;
    private readonly Func<int, bool> wheel;
    private readonly Func<bool> canScroll;
    private readonly HashSet<IntPtr> attached = new();
    private readonly uint thread = GetCurrentThreadId();

    internal bool RawRegistered { get; }

    internal NativeWheelInput(IntPtr window, Func<int, bool> wheel, Func<bool> canScroll)
    {
        this.window = window;
        this.wheel = wheel;
        this.canScroll = canScroll;
        callback = Process;
        Refresh();
        // Flags=0: foreground input only. No global hook, background capture,
        // or suppression of legacy button/movement messages.
        RawRegistered = RegisterRawInputDevices(
            new[] { new RawDevice { UsagePage = 1, Usage = 2, Window = window } },
            1, (uint)Marshal.SizeOf<RawDevice>());
        ScrollDiagnostics.Write($"raw registration={RawRegistered} error={Marshal.GetLastWin32Error()}");
    }

    internal void Refresh()
    {
        Attach(window);
        EnumChildWindows(window, (child, _) => { Attach(child); return true; }, IntPtr.Zero);
    }

    private void Attach(IntPtr handle)
    {
        if (attached.Contains(handle)) return;
        var name = new StringBuilder(256);
        GetClassNameW(handle, name, name.Capacity);
        uint owner = GetWindowThreadProcessId(handle, out _);
        if (owner != thread)
        {
            ScrollDiagnostics.Write($"skip window={handle:X} class={name} owner={owner} uiThread={thread}");
            return; // SetWindowSubclass must never be used across threads.
        }
        bool success = SetWindowSubclass(handle, callback, SubclassId, UIntPtr.Zero);
        ScrollDiagnostics.Write($"subclass window={handle:X} class={name} success={success}");
        if (success) attached.Add(handle);
    }

    private IntPtr Process(IntPtr handle, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        try
        {
            if (message == 0x00ff /* WM_INPUT */ && handle == window) ReadRawWheel(lParam);
            if (message == 0x020A || message == 0x024E)
            {
                int delta = unchecked((short)((wParam.ToUInt64() >> 16) & 0xffff));
                ScrollDiagnostics.Write($"wndproc window={handle:X} message={message:X} delta={delta}");
                if (delta != 0 && canScroll() && (RawRegistered || wheel(delta))) return IntPtr.Zero;
            }
            if (message == 0x0082 /* WM_NCDESTROY */)
            {
                RemoveWindowSubclass(handle, callback, SubclassId);
                attached.Remove(handle);
            }
        }
        catch (Exception error) { App.LogStartupError(error); }
        return DefSubclassProc(handle, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (RawRegistered) RegisterRawInputDevices(
            new[] { new RawDevice { UsagePage = 1, Usage = 2, Flags = 1 /* RIDEV_REMOVE */ } },
            1, (uint)Marshal.SizeOf<RawDevice>());
        foreach (var handle in attached.ToArray()) RemoveWindowSubclass(handle, callback, SubclassId);
        attached.Clear();
        GC.KeepAlive(callback);
    }

    private unsafe void ReadRawWheel(IntPtr input)
    {
        // RAWINPUTHEADER + RAWMOUSE is 48 bytes on x64. A fixed stack buffer
        // avoids a heap allocation and size-query call for every mouse movement,
        // particularly useful with high-polling-rate gaming mice.
        byte* storage = stackalloc byte[64];
        IntPtr buffer = (IntPtr)storage;
        uint size = 64, headerSize = (uint)(8 + IntPtr.Size * 2);
        uint read = GetRawInputData(input, 0x10000003, buffer, ref size, headerSize);
        if (read == uint.MaxValue || read < headerSize + 8
            || Marshal.ReadInt32(buffer) != 0 /* RIM_TYPEMOUSE */) return;
        int buttons = (ushort)Marshal.ReadInt16(buffer, (int)headerSize + 4);
        if ((buttons & 0x0400 /* RI_MOUSE_WHEEL */) == 0) return;
        if (!GetCursorPos(out var cursor) || GetAncestor(WindowFromPoint(cursor), 2 /* GA_ROOT */) != window) return;
        int delta = Marshal.ReadInt16(buffer, (int)headerSize + 6);
        ScrollDiagnostics.Write($"raw wheel delta={delta} allowed={canScroll()}");
        if (delta != 0 && canScroll()) wheel(delta);
        // Always continue WM_INPUT to DefWindowProc for foreground cleanup.
    }

    [StructLayout(LayoutKind.Sequential)] private struct RawDevice
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr Window;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }

    private delegate IntPtr SubclassProc(IntPtr handle, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);
    private delegate bool EnumProc(IntPtr handle, IntPtr data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr handle, SubclassProc proc, uint id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr handle, SubclassProc proc, uint id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr handle, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumProc proc, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr handle, StringBuilder name, int capacity);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterRawInputDevices([In] RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr buffer, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr handle, uint flags);
}
