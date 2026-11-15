using static Luma.UiText;
// XboxGIP protocol adapted from xbledctl (MIT), © 2026 Leclowndu93150.
// See THIRD-PARTY-NOTICES.txt. No replacement driver or input emulation.
using System.Buffers.Binary;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Luma;

public static class LedPacket
{
    public static byte[] Create(ulong device, byte sequence, byte mode, int percent)
    {
        if (device == 0) throw new ArgumentOutOfRangeException(nameof(device));
        if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (!new byte[] { 0, 1, 2, 3, 4, 8, 9, 13 }.Contains(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (percent < 0 || percent > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        var packet = new byte[23];
        BinaryPrimitives.WriteUInt64LittleEndian(packet, device);
        packet[8] = 0x0A; packet[9] = 0x20; packet[10] = sequence;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), 3);
        packet[21] = percent == 0 ? (byte)0 : mode;
        // UI 0–100% maps to the supported protocol range 0–47.
        packet[22] = mode == 0 ? (byte)0 : (byte)Math.Round(percent * 47.0 / 100, MidpointRounding.AwayFromZero);
        return packet;
    }
}

public sealed class GipController : IDisposable
{
    private SafeFileHandle? handle;
    private byte sequence = 1;
    public IReadOnlyList<ulong> Discover()
    {
        if (handle == null || handle.IsInvalid || handle.IsClosed)
        {
            handle = CreateFileW(@"\\.\XboxGIP", 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
            if (handle.IsInvalid) { int code = Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(code, T("XboxGIP ist nicht erreichbar. Controller über USB oder Xbox Wireless Adapter verbinden.")); }
        }
        Io(2, Array.Empty<byte>(), 1000);
        var ids = new HashSet<ulong>();
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < 1600)
        {
            var data = new byte[4096];
            int read = Io(0, data, ids.Count == 0 ? 450u : 200u, true);
            if (read == 0) break;
            if (read >= 20 && (data[8] == 1 || data[8] == 2))
            {
                ulong id = BinaryPrimitives.ReadUInt64LittleEndian(data);
                if (id != 0) ids.Add(id);
            }
        }
        return ids.Order().ToArray();
    }
    public void SetLed(ulong id, bool enabled, byte mode, int percent)
    {
        if (handle == null || handle.IsInvalid) throw new InvalidOperationException(T("Kein Controller verbunden."));
        var packet = LedPacket.Create(id, sequence, enabled ? mode : (byte)0, percent);
        sequence = sequence == 255 ? (byte)1 : (byte)(sequence + 1);
        if (Io(1, packet, 1500) != packet.Length) throw new IOException(T("LED-Befehl wurde nicht vollständig übertragen."));
    }

    // Own the event, OVERLAPPED and pinned buffer until completion, including cancellation.
    private int Io(int kind, byte[] buffer, uint timeout, bool allowTimeout = false)
    {
        using var completed = new EventWaitHandle(false, EventResetMode.ManualReset);
        var overlapped = new OverlappedData { Event = completed.SafeWaitHandle.DangerousGetHandle() };
        IntPtr native = Marshal.AllocHGlobal(Marshal.SizeOf<OverlappedData>());
        GCHandle pinned = default;
        try
        {
            Marshal.StructureToPtr(overlapped, native, false);
            IntPtr address = IntPtr.Zero;
            if (buffer.Length != 0) { pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned); address = pinned.AddrOfPinnedObject(); }
            uint count;
            bool ok = kind switch
            {
                0 => ReadFile(handle!, address, (uint)buffer.Length, out count, native),
                1 => WriteFile(handle!, address, (uint)buffer.Length, out count, native),
                _ => DeviceIoControl(handle!, 0x40001CD0, IntPtr.Zero, 0, IntPtr.Zero, 0, out count, native)
            };
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 997) throw new Win32Exception(error);
                if (!completed.WaitOne((int)timeout))
                {
                    CancelIoEx(handle!, native);
                    // Wait for cancellation before freeing the OVERLAPPED/buffer.
                    GetOverlappedResult(handle!, native, out count, true);
                    if (allowTimeout) return 0;
                    throw new TimeoutException(T("Der Controller antwortet nicht. Bitte erneut verbinden."));
                }
                if (!GetOverlappedResult(handle!, native, out count, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            return (int)count;
        }
        finally { if (pinned.IsAllocated) pinned.Free(); Marshal.FreeHGlobal(native); }
    }
    public void Dispose() { handle?.Dispose(); handle = null; }
    [StructLayout(LayoutKind.Sequential)] private struct OverlappedData { public nuint Internal; public nuint InternalHigh; public uint Offset; public uint OffsetHigh; public IntPtr Event; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle h, IntPtr data, uint size, out uint read, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(SafeFileHandle h, IntPtr data, uint size, out uint written, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr input, uint inputSize, IntPtr output, uint outputSize, out uint count, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetOverlappedResult(SafeFileHandle h, IntPtr ov, out uint count, [MarshalAs(UnmanagedType.Bool)] bool wait);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CancelIoEx(SafeFileHandle h, IntPtr ov);
}
