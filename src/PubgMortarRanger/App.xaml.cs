using System.Windows.Interop;
using System.Windows;
using System.IO;
using PubgMortarRanger.Configuration;
using PubgMortarRanger.Input;
using PubgMortarRanger.Voice;
using PubgMortarRanger.Workflow;

namespace PubgMortarRanger;

public partial class App : System.Windows.Application
{
    private const int WmHotkey = 0x0312;
    private readonly RangingController _controller = new();
    private readonly CursorPositionService _cursor = new();
    private readonly VoiceAnnouncementController _voiceAnnouncement =
        new(new SapiVoiceAnnouncementService());
    private GlobalHotkeyService? _hotkeys;
    private SettingsService? _settingsService;
    private AppSettings? _settings;
    private MainWindow? _window;
    private readonly SelectionOverlayManager _selection = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PubgMortarRanger");
        _settingsService = new SettingsService(directory);
        var settings = await _settingsService.LoadAsync();
        _settings = settings;
        _controller.UpdateRangeLimits(settings.MinimumRangeMeters, settings.MaximumRangeMeters);
        _controller.SetCalibration(settings.Calibration);

        _window = new MainWindow(_controller);
        _window.UpdateHotkeyHint(settings.Hotkeys);
        _window.SourceInitialized += (_, _) => RegisterHotkeys(settings.Hotkeys);
        _window.SettingsRequested += async (_, _) => await ShowSettingsAsync();
        _window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _selection.Dispose();
        base.OnExit(e);
    }

    private void RegisterHotkeys(IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        var source = (HwndSource)PresentationSource.FromVisual(_window!)!;
        source.AddHook(WindowMessageHook);
        _hotkeys = new GlobalHotkeyService(new WindowsHotkeyRegistrar(source.Handle));
        var result = _hotkeys.Apply(bindings);
        if (!result.IsValid)
        {
            System.Windows.MessageBox.Show(
                result.ErrorMessage,
                "热键注册失败");
        }
    }

    private nint WindowMessageHook(
        nint handle,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message != WmHotkey)
        {
            return nint.Zero;
        }

        handled = true;
        var action = (HotkeyAction)((int)wParam - 1);
        HandleHotkey(action);
        return nint.Zero;
    }

    private void HandleHotkey(HotkeyAction action)
    {
        var point = _cursor.GetPhysicalPosition();
        switch (action)
        {
            case HotkeyAction.BeginCalibration:
                _controller.BeginCalibration();
                _selection.Close();
                break;
            case HotkeyAction.RecordMortar:
                if (_controller.State is RangingState.AwaitingCalibrationFirstPoint or RangingState.AwaitingCalibrationSecondPoint)
                {
                    _controller.RecordPoint(point);
                }
                else
                {
                    _controller.RecordMortar(point);
                }
                _selection.SetGuideSegment(_controller.GuideSegment);
                break;
            case HotkeyAction.RecordTarget:
                if (_controller.State == RangingState.AwaitingCalibrationSecondPoint)
                {
                    _controller.RecordPoint(point);
                    _selection.SetGuideSegment(_controller.GuideSegment);
                    _window?.ShowCalibrationDistancePrompt();
                }
                else
                {
                    _controller.RecordTarget(point);
                    _selection.SetGuideSegment(_controller.GuideSegment);
                }
                break;
            case HotkeyAction.ClearMeasurement:
                _controller.ClearMeasurement();
                _selection.Close();
                break;
            case HotkeyAction.BeginClickSelection:
                BeginPointSelection(forceCalibration: false);
                break;
            case HotkeyAction.Recalibrate:
                BeginPointSelection(forceCalibration: true);
                break;
            case HotkeyAction.ToggleOverlay:
                if (_window is not null)
                {
                    _window.Visibility = _window.IsVisible
                        ? Visibility.Hidden
                        : Visibility.Visible;
                }
                break;
            case HotkeyAction.CancelCurrent:
                _controller.Cancel();
                _selection.Close();
                break;
            case HotkeyAction.PlayVoiceAnnouncement:
                _voiceAnnouncement.PlayIfEnabled(
                    _settings?.VoiceAnnouncementEnabled ?? false);
                break;
        }
    }

    private void BeginPointSelection(bool forceCalibration)
    {
        _selection.PointSelected -= OnSelectionPoint;
        _selection.PointSelected += OnSelectionPoint;
        if (forceCalibration || _controller.Calibration is null)
        {
            _controller.BeginCalibration();
        }
        else
        {
            _controller.BeginClickMeasurement();
        }

        _selection.Show();
    }

    private async Task ShowSettingsAsync()
    {
        if (_settings is null || _settingsService is null || _window is null)
        {
            return;
        }

        _hotkeys?.Suspend();
        try
        {
            var dialog = new SettingsWindow(_settings) { Owner = _window };
            if (dialog.ShowDialog() != true || dialog.Result is not { } candidate)
            {
                return;
            }

            var result = _hotkeys?.Apply(candidate.Hotkeys);
            if (result is { IsValid: false })
            {
                System.Windows.MessageBox.Show(result.ErrorMessage, "热键注册失败");
                return;
            }

            _controller.UpdateRangeLimits(candidate.MinimumRangeMeters, candidate.MaximumRangeMeters);
            await _settingsService.SaveAsync(candidate);
            _settings = candidate;
            _window.UpdateHotkeyHint(candidate.Hotkeys);
        }
        finally
        {
            var resumeResult = _hotkeys?.Resume();
            if (resumeResult is { IsValid: false })
            {
                System.Windows.MessageBox.Show(resumeResult.ErrorMessage, "热键注册失败");
            }
        }
    }

    private void OnSelectionPoint(object? sender, Core.ScreenPoint point)
    {
        var result = _controller.RecordPoint(point);
        _selection.SetGuideSegment(_controller.GuideSegment);
        if (_controller.State == RangingState.AwaitingCalibrationDistance)
        {
            _selection.CloseSelection();
            _window?.ShowCalibrationDistancePrompt();
            return;
        }

        if (result is not null)
        {
            _selection.CloseSelection();
        }
    }
}
