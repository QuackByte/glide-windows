using System.Diagnostics;
using System.Runtime.InteropServices;
using Glide.Core;
using Windows.Win32;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input;

namespace Glide.Platform;

/// <summary>
/// Reads finger positions from precision touchpads, through raw input in the background, and
/// turns them into frames of touches for <see cref="SwipeTracker"/>. Windows doesn't offer
/// custom touchpad gestures, so this reads the touchpad's standard HID reports: each finger is
/// a link collection with X, Y and a tip switch, and the report says how many fingers are down.
/// </summary>
internal sealed unsafe class Touchpad
{
    public const uint WM_INPUT = 0x00FF;
    private const ushort DigitizerPage = 0x0D, GenericDesktopPage = 0x01;
    private const ushort TouchPadUsage = 0x05, TipSwitch = 0x42, ContactCount = 0x54, ContactId = 0x51, X = 0x30, Y = 0x31;

    /// <summary>What a touchpad's reports look like, read once per device.</summary>
    private sealed record Device(byte[] PreparsedData, IReadOnlyList<(ushort Collection, int MaxX, int MaxY)> Fingers, ushort CountCollection);

    private readonly Dictionary<nint, Device?> devices = [];
    private readonly Action<IReadOnlyList<Touch>, double> onFrame;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    /// <summary>Fingers of the frame being assembled: touchpads in hybrid mode spread a frame over several reports.</summary>
    private readonly Dictionary<uint, Touch> pending = [];
    private int expected;

    /// <param name="onFrame">Every finger down, normalized to 0…1, and the time in seconds.</param>
    public Touchpad(Action<IReadOnlyList<Touch>, double> onFrame) => this.onFrame = onFrame;

    /// <summary>Asks for touchpad input sent to <paramref name="window"/>, even while other apps have focus.</summary>
    public static bool Register(nint window, bool on)
    {
        var device = new RAWINPUTDEVICE
        {
            usUsagePage = DigitizerPage,
            usUsage = TouchPadUsage,
            // RIDEV_INPUTSINK: in the background too. RIDEV_REMOVE stops it.
            dwFlags = on ? (RAWINPUTDEVICE_FLAGS)0x100 : (RAWINPUTDEVICE_FLAGS)0x1,
            hwndTarget = on ? new HWND((void*)window) : HWND.Null,
        };
        var ok = PInvoke.RegisterRawInputDevices(&device, 1, (uint)sizeof(RAWINPUTDEVICE));
        if (!ok && on) Log.Error($"Couldn't listen to the touchpad: {Marshal.GetLastPInvokeErrorMessage()}");
        return ok;
    }

    /// <summary>Handles WM_INPUT.</summary>
    public void Handle(nint lParam)
    {
        var input = new HRAWINPUT((void*)lParam);
        uint size = 0;
        PInvoke.GetRawInputData(input, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, null, &size, (uint)sizeof(RAWINPUTHEADER));
        if (size == 0) return;
        var buffer = stackalloc byte[(int)size];
        if (PInvoke.GetRawInputData(input, RAW_INPUT_DATA_COMMAND_FLAGS.RID_INPUT, buffer, &size, (uint)sizeof(RAWINPUTHEADER)) != size) return;
        var raw = (RAWINPUT*)buffer;
        if (raw->header.dwType != 2 /* RIM_TYPEHID */) return;
        if (DeviceFor(raw->header.hDevice) is not { } device) return;

        var hid = &raw->data.hid;
        var reports = (byte*)&hid->bRawData;
        for (var i = 0; i < hid->dwCount; i++)
            Read(device, reports + i * hid->dwSizeHid, hid->dwSizeHid);
    }

    private void Read(Device device, byte* report, uint length)
    {
        fixed (byte* preparsed = device.PreparsedData)
        {
            var data = (PHIDP_PREPARSED_DATA)(nint)preparsed;
            uint count;
            if (Value(data, DigitizerPage, device.CountCollection, ContactCount, report, length, &count) && count > 0)
            {
                // A new frame starts with its finger count; later reports of the same frame say 0.
                pending.Clear();
                expected = (int)count;
            }
            foreach (var (collection, maxX, maxY) in device.Fingers)
            {
                uint x, y, id;
                if (!Value(data, GenericDesktopPage, collection, X, report, length, &x) || !Value(data, GenericDesktopPage, collection, Y, report, length, &y)) continue;
                if (!Value(data, DigitizerPage, collection, ContactId, report, length, &id)) id = collection;
                if (!IsTipDown(data, collection, report, length)) continue;
                pending[id] = new Touch(x / (double)Math.Max(maxX, 1), 1 - y / (double)Math.Max(maxY, 1));
            }
        }
        if (expected <= 0) return;
        // Frames with fingers still to come wait for their other reports; a frame with every
        // finger lifted (expected fingers, none down) still ends a swipe.
        var reported = Math.Min(expected, device.Fingers.Count);
        if (pending.Count < reported && pending.Count != 0) return;
        onFrame([.. pending.Values], clock.Elapsed.TotalSeconds);
        pending.Clear();
        expected = 0;
    }

    private static bool Value(PHIDP_PREPARSED_DATA data, ushort page, ushort collection, ushort usage, byte* report, uint length, uint* value) =>
        PInvoke.HidP_GetUsageValue(HIDP_REPORT_TYPE.HidP_Input, page, collection, usage, value, data, new PSTR(report), length).Value == 0x00110000; // HIDP_STATUS_SUCCESS

    private static bool IsTipDown(PHIDP_PREPARSED_DATA data, ushort collection, byte* report, uint length)
    {
        var usages = stackalloc ushort[16];
        uint count = 16;
        if (PInvoke.HidP_GetUsages(HIDP_REPORT_TYPE.HidP_Input, DigitizerPage, collection, usages, &count, data, new PSTR(report), length).Value != 0x00110000)
            return false;
        for (var i = 0; i < count; i++)
            if (usages[i] == TipSwitch) return true;
        return false;
    }

    /// <summary>Learns how a touchpad reports fingers; null (and remembered) if it isn't one Glide can read.</summary>
    private Device? DeviceFor(HANDLE handle)
    {
        if (devices.TryGetValue((nint)handle.Value, out var known)) return known;
        Device? device = null;
        try
        {
            uint size = 0;
            PInvoke.GetRawInputDeviceInfo(handle, RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_PREPARSEDDATA, null, &size);
            var preparsed = new byte[size];
            fixed (byte* bytes = preparsed)
            {
                if (size > 0 && PInvoke.GetRawInputDeviceInfo(handle, RAW_INPUT_DEVICE_INFO_COMMAND.RIDI_PREPARSEDDATA, bytes, &size) == size)
                {
                    var data = (PHIDP_PREPARSED_DATA)(nint)bytes;
                    HIDP_CAPS caps;
                    PInvoke.HidP_GetCaps(data, &caps);
                    var valueCaps = new HIDP_VALUE_CAPS[caps.NumberInputValueCaps];
                    var count = caps.NumberInputValueCaps;
                    fixed (HIDP_VALUE_CAPS* values = valueCaps)
                        PInvoke.HidP_GetValueCaps(HIDP_REPORT_TYPE.HidP_Input, values, &count, data);
                    var fingers = new List<(ushort, int, int)>();
                    ushort countCollection = 0;
                    var available = valueCaps.Take(count).ToArray();
                    foreach (var collection in available.Select(v => v.LinkCollection).Distinct())
                    {
                        int Max(ushort page, ushort usage) => available
                            .Where(v => v.LinkCollection == collection && v.UsagePage == page && Covers(v, usage)).Select(v => v.LogicalMax).DefaultIfEmpty(-1).First();
                        var maxX = Max(GenericDesktopPage, X);
                        var maxY = Max(GenericDesktopPage, Y);
                        if (maxX > 0 && maxY > 0) fingers.Add((collection, maxX, maxY));
                        if (Max(DigitizerPage, ContactCount) >= 0) countCollection = collection;
                    }
                    if (fingers.Count > 0) device = new Device(preparsed, fingers, countCollection);
                    Log.Info(device is null ? "Touchpad: a device Glide can't read fingers from" : $"Touchpad: {fingers.Count} fingers per report");
                }
            }
        }
        catch (Exception e)
        {
            Log.Error("Touchpad: reading its description failed", e);
        }
        devices[(nint)handle.Value] = device;
        return device;
    }

    private static bool Covers(HIDP_VALUE_CAPS caps, ushort usage) =>
        caps.IsRange ? usage >= caps.Anonymous.Range.UsageMin && usage <= caps.Anonymous.Range.UsageMax : caps.Anonymous.NotRange.Usage == usage;
}
