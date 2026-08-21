using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using PubgMortarRanger.Core;
using PubgMortarRanger.Interop;
using MediaBrushes = System.Windows.Media.Brushes;

namespace PubgMortarRanger;

public sealed class GuideOverlayWindow : Window
{
    private readonly Screen _screen;
    private readonly Line _line;

    public GuideOverlayWindow(Screen screen)
    {
        _screen = screen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = MediaBrushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        Left = screen.Bounds.Left;
        Top = screen.Bounds.Top;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;

        _line = new Line
        {
            Stroke = MediaBrushes.Yellow,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            IsHitTestVisible = false,
            SnapsToDevicePixels = true
        };
        Content = new Canvas
        {
            Background = MediaBrushes.Transparent,
            IsHitTestVisible = false,
            Children = { _line }
        };
        SourceInitialized += (_, _) => EnableMousePassThrough();
    }

    public void SetSegment(GuideSegment segment)
    {
        _line.X1 = segment.Start.X - _screen.Bounds.Left;
        _line.Y1 = segment.Start.Y - _screen.Bounds.Top;
        _line.X2 = segment.End.X - _screen.Bounds.Left;
        _line.Y2 = segment.End.Y - _screen.Bounds.Top;
    }

    private void EnableMousePassThrough()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLongPtr(
            handle,
            NativeMethods.GwlExStyle).ToInt64();
        var updatedStyle = extendedStyle |
            NativeMethods.WsExTransparent |
            NativeMethods.WsExToolWindow |
            NativeMethods.WsExNoActivate;
        NativeMethods.SetWindowLongPtr(
            handle,
            NativeMethods.GwlExStyle,
            new nint(updatedStyle));
    }
}
