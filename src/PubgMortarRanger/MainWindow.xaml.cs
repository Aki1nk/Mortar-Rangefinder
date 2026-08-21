using System.Windows;
using System.Windows.Input;
using PubgMortarRanger.Core;
using PubgMortarRanger.Input;
using PubgMortarRanger.Workflow;

namespace PubgMortarRanger;

public partial class MainWindow : Window
{
    private readonly RangingController _controller;
    private IReadOnlyDictionary<HotkeyAction, HotkeyGesture> _hotkeys =
        HotkeyGesture.CreateDefaults();

    public MainWindow(RangingController controller)
    {
        _controller = controller;
        InitializeComponent();
        _controller.Changed += (_, _) => Dispatcher.Invoke(UpdateDisplay);
        UpdateDisplay();
    }

    public event EventHandler? SettingsRequested;

    public void UpdateHotkeyHint(IReadOnlyDictionary<HotkeyAction, HotkeyGesture> hotkeys)
    {
        _hotkeys = hotkeys;
        UpdateDisplay();
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    private void OnMinimizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Close();

    private void OnWindowMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button)
        {
            return;
        }

        DragMove();
    }

    public void ShowCalibrationDistancePrompt()
    {
        var dialog = new Window
        {
            Title = "标定距离（米）",
            Width = 260,
            Height = 130,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var input = new System.Windows.Controls.TextBox { Text = "500", Margin = new Thickness(12) };
        var button = new System.Windows.Controls.Button { Content = "确认", Margin = new Thickness(12), IsDefault = true };
        button.Click += (_, _) =>
        {
            if (double.TryParse(input.Text, out var meters) && meters > 0)
            {
                dialog.Tag = meters;
                dialog.DialogResult = true;
            }
        };
        var panel = new System.Windows.Controls.StackPanel();
        panel.Children.Add(input);
        panel.Children.Add(button);
        dialog.Content = panel;
        if (dialog.ShowDialog() == true && dialog.Tag is double meters)
        {
            _controller.CompleteCalibration(meters, "manual-display");
        }
    }

    private void UpdateDisplay()
    {
        if (_controller.LastMeasurement is { } result)
        {
            DistanceText.Text = $"{Math.Round(result.DistanceMeters):0} m";
            BearingText.Text = $"{Math.Round(result.BearingDegrees):000}°";
            RangeText.Text = result.RangeStatus switch
            {
                RangeStatus.TooClose => "过近",
                RangeStatus.TooFar => "过远",
                _ => "射程内"
            };
        }
        else
        {
            DistanceText.Text = "--- m";
            BearingText.Text = "---°";
            RangeText.Text = _controller.State == RangingState.Uncalibrated ? "未标定" : "待测量";
        }

        HintText.Text = _controller.State switch
        {
            RangingState.AwaitingCalibrationFirstPoint => $"移动鼠标到标定点 1，按 {HotkeyText(HotkeyAction.RecordMortar)}",
            RangingState.AwaitingCalibrationSecondPoint => $"移动鼠标到标定点 2，按 {HotkeyText(HotkeyAction.RecordTarget)}",
            RangingState.AwaitingCalibrationDistance => "输入这两个点的实际距离",
            RangingState.AwaitingTargetPoint => $"移动鼠标到目标，按 {HotkeyText(HotkeyAction.RecordTarget)}",
            _ => $"{HotkeyText(HotkeyAction.BeginClickSelection)} 两次点击标定/测距 | " +
                 $"{HotkeyText(HotkeyAction.Recalibrate)} 重新标定 | " +
                 $"{HotkeyText(HotkeyAction.ClearMeasurement)} 清除"
        };
    }

    private string HotkeyText(HotkeyAction action)
    {
        return HotkeyDisplayFormatter.Format(_hotkeys[action]);
    }
}
