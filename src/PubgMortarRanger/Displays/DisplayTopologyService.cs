using System.Windows.Forms;
using PubgMortarRanger.Core;
using PubgMortarRanger.Interop;

namespace PubgMortarRanger.Displays;

public sealed class DisplayTopologyService
{
    public IReadOnlyList<DisplayFingerprint> Capture() =>
        Screen.AllScreens
            .Select(CreateFingerprint)
            .OrderBy(fingerprint => fingerprint.DeviceName, StringComparer.Ordinal)
            .ToArray();

    public DisplayFingerprint FindForPoint(ScreenPoint physicalPoint)
    {
        var monitorHandle = NativeMethods.MonitorFromPoint(
            ToNativePoint(physicalPoint),
            NativeMethods.MonitorDefaultToNearest);

        if (monitorHandle == nint.Zero)
        {
            throw new InvalidOperationException("无法找到鼠标所在的显示器。");
        }

        return CreateFingerprint(Screen.FromHandle(monitorHandle));
    }

    public string CaptureStableValueForPoint(ScreenPoint physicalPoint)
    {
        var currentDisplay = FindForPoint(physicalPoint);
        var topology = Capture();

        return $"{currentDisplay.StableValue};{string.Join(
            ";",
            topology.Select(fingerprint => fingerprint.StableValue))}";
    }

    private static DisplayFingerprint CreateFingerprint(Screen screen)
    {
        var monitorHandle = NativeMethods.MonitorFromPoint(
            ToNativePoint(
                screen.Bounds.Left + (screen.Bounds.Width / 2d),
                screen.Bounds.Top + (screen.Bounds.Height / 2d)),
            NativeMethods.MonitorDefaultToNearest);

        if (monitorHandle == nint.Zero ||
            NativeMethods.GetDpiForMonitor(
                monitorHandle,
                NativeMethods.MdtEffectiveDpi,
                out var dpiX,
                out var dpiY) != 0)
        {
            throw new InvalidOperationException("无法读取显示器 DPI。");
        }

        return new DisplayFingerprint(
            screen.DeviceName,
            screen.Bounds.Left,
            screen.Bounds.Top,
            screen.Bounds.Width,
            screen.Bounds.Height,
            dpiX,
            dpiY);
    }

    private static NativeMethods.Point ToNativePoint(ScreenPoint point) =>
        ToNativePoint(point.X, point.Y);

    private static NativeMethods.Point ToNativePoint(double x, double y) =>
        new(checked((int)Math.Round(x)), checked((int)Math.Round(y)));
}
