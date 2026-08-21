using System.Windows.Forms;
using System.Windows.Threading;
using PubgMortarRanger.Core;

namespace PubgMortarRanger;

public sealed class SelectionOverlayManager : IDisposable
{
    private readonly List<SelectionOverlayWindow> _windows = [];
    private readonly List<GuideOverlayWindow> _guideWindows = [];
    private readonly DispatcherTimer _guideTimer;

    internal static readonly TimeSpan GuideDisplayDuration = TimeSpan.FromSeconds(2);

    public SelectionOverlayManager()
    {
        _guideTimer = new DispatcherTimer
        {
            Interval = GuideDisplayDuration
        };
        _guideTimer.Tick += (_, _) => ClearGuide();
    }

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
        CloseSelection();
        ClearGuide();
    }

    public void CloseSelection()
    {
        foreach (var window in _windows) window.Close();
        _windows.Clear();
    }

    public void SetGuideSegment(GuideSegment? segment)
    {
        ClearGuide();
        if (segment is null)
        {
            return;
        }

        foreach (var screen in Screen.AllScreens)
        {
            var window = new GuideOverlayWindow(screen);
            window.SetSegment(segment);
            _guideWindows.Add(window);
            window.Show();
        }

        _guideTimer.Start();
    }

    private void ClearGuide()
    {
        _guideTimer.Stop();
        foreach (var window in _guideWindows) window.Close();
        _guideWindows.Clear();
    }

    public void Dispose() => Close();
}
