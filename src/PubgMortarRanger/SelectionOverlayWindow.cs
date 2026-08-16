using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using PubgMortarRanger.Core;

namespace PubgMortarRanger;

public sealed class SelectionOverlayWindow : Window
{
    private readonly Screen _screen;

    public SelectionOverlayWindow(Screen screen)
    {
        _screen = screen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(20, 80, 220, 120));
        Topmost = true;
        ShowInTaskbar = false;
        Left = screen.Bounds.Left;
        Top = screen.Bounds.Top;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;
        Cursor = System.Windows.Input.Cursors.Cross;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
    }

    public event EventHandler<ScreenPoint>? PointSelected;

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        PointSelected?.Invoke(this, new ScreenPoint(_screen.Bounds.Left + position.X, _screen.Bounds.Top + position.Y));
    }
}
