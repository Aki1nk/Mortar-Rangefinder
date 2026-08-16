using System.Windows.Forms;
using PubgMortarRanger.Core;

namespace PubgMortarRanger;

public sealed class SelectionOverlayManager : IDisposable
{
    private readonly List<SelectionOverlayWindow> _windows = [];
    public event EventHandler<ScreenPoint>? PointSelected;

    public void Show()
    {
        Close();
        foreach (var screen in Screen.AllScreens)
        {
            var window = new SelectionOverlayWindow(screen);
            window.PointSelected += (_, point) => PointSelected?.Invoke(this, point);
            _windows.Add(window);
            window.Show();
        }
    }

    public void Close()
    {
        foreach (var window in _windows) window.Close();
        _windows.Clear();
    }

    public void Dispose() => Close();
}
