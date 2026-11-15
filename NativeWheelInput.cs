using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Luma;

// Observe only this app's UI-thread queue, before XAML can consume wheel input.
// There is no global mouse hook and no observation of other applications.
internal sealed class NativeWheelInput : IDisposable
{
    private readonly HookProc callback;
    private readonly IntPtr window;
    private readonly Func<int, bool> wheel;
    private IntPtr hook;

    internal NativeWheelInput(IntPtr window, Func<int, bool> wheel)
    {
        this.window = window;
        this.wheel = wheel;
        callback = Process;
        hook = SetWindowsHookExW(3 /* WH_GETMESSAGE */, callback, IntPtr.Zero, GetCurrentThreadId());
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr Process(int code, UIntPtr remove, IntPtr address)
    {
        if (code >= 0 && remove.ToUInt64() == 1 /* PM_REMOVE */)
        {
            try
            {
                var message = Marshal.PtrToStructure<Message>(address);
                if ((message.Id == 0x020A /* WM_MOUSEWHEEL */ || message.Id == 0x024E /* WM_POINTERWHEEL */)
                    && (message.Window == window || IsChild(window, message.Window)))
                {
                    int delta = unchecked((short)((message.WParam.ToUInt64() >> 16) & 0xffff));
                    if (delta != 0 && wheel(delta))
                    {
                        // Removing it from dispatch also prevents native/XAML promotion
                        // from delivering the same physical packet a second time.
                        message.Id = 0; // WM_NULL
                        message.WParam = UIntPtr.Zero;
                        message.LParam = IntPtr.Zero;
                        Marshal.StructureToPtr(message, address, false);
                    }
                }
            }
            catch (Exception error) { App.LogStartupError(error); }
        }
        return CallNextHookEx(hook, code, remove, address);
    }

    public void Dispose()
    {
        if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        GC.KeepAlive(callback);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }
    private delegate IntPtr HookProc(int code, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookExW(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
