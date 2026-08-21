using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PubgMortarRanger.Configuration;
using PubgMortarRanger.Input;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Button = System.Windows.Controls.Button;
using CheckBox = System.Windows.Controls.CheckBox;
using MessageBox = System.Windows.MessageBox;

namespace PubgMortarRanger;

public sealed class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Dictionary<HotkeyAction, HotkeyGesture> _hotkeys;
    private readonly Dictionary<HotkeyAction, TextBox> _hotkeyEditors = [];
    private readonly TextBox _minimumRange = new();
    private readonly TextBox _maximumRange = new();
    private readonly CheckBox _voiceAnnouncementEnabled = new();

    public SettingsWindow(AppSettings settings)
    {
        _settings = settings;
        _hotkeys = settings.Hotkeys.ToDictionary();
        Title = "Mortar Rangefinder 设置";
        Width = 420;
        Height = 570;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "射程设置", FontWeight = FontWeights.Bold });
        panel.Children.Add(CreateLabeledEditor("最小射程（米）", _minimumRange, settings.MinimumRangeMeters.ToString("0")));
        panel.Children.Add(CreateLabeledEditor("最大射程（米）", _maximumRange, settings.MaximumRangeMeters.ToString("0")));
        panel.Children.Add(new TextBlock { Text = "语音提示", Margin = new Thickness(0, 12, 0, 4), FontWeight = FontWeights.Bold });
        _voiceAnnouncementEnabled.Content = "启用本地语音提示（不会操作游戏）";
        _voiceAnnouncementEnabled.IsChecked = settings.VoiceAnnouncementEnabled;
        panel.Children.Add(_voiceAnnouncementEnabled);
        panel.Children.Add(new TextBlock { Text = "热键设置（点击输入框后按组合键）", Margin = new Thickness(0, 12, 0, 4), FontWeight = FontWeights.Bold });

        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            var editor = new TextBox { Text = Format(_hotkeys[action]), IsReadOnly = true, MinWidth = 140 };
            editor.PreviewKeyDown += (_, args) => CaptureHotkey(action, editor, args);
            _hotkeyEditors[action] = editor;
            panel.Children.Add(CreateLabeledEditor(ActionName(action), editor, null));
        }

        var save = new Button { Content = "保存", Margin = new Thickness(0, 14, 0, 0), IsDefault = true };
        save.Click += (_, _) => Save();
        panel.Children.Add(save);
        Content = new ScrollViewer { Content = panel };
    }

    public AppSettings? Result { get; private set; }

    private static FrameworkElement CreateLabeledEditor(string label, TextBox editor, string? text)
    {
        if (text is not null) editor.Text = text;
        var grid = new Grid { Margin = new Thickness(0, 3, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);
        return grid;
    }

    private void CaptureHotkey(HotkeyAction action, TextBox editor, KeyEventArgs args)
    {
        var key = args.Key == Key.System ? args.SystemKey : args.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
        {
            return;
        }
        args.Handled = true;
        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= HotkeyModifiers.Control;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= HotkeyModifiers.Windows;
        _hotkeys[action] = new HotkeyGesture(modifiers, KeyInterop.VirtualKeyFromKey(key), action != HotkeyAction.CancelCurrent);
        editor.Text = Format(_hotkeys[action]);
    }

    private void Save()
    {
        if (!double.TryParse(_minimumRange.Text, out var minimum) ||
            !double.TryParse(_maximumRange.Text, out var maximum))
        {
            MessageBox.Show("射程必须是数字。", "设置");
            return;
        }

        var validation = HotkeyValidator.Validate(_hotkeys);
        if (!validation.IsValid)
        {
            MessageBox.Show(validation.ErrorMessage, "设置");
            return;
        }

        Result = _settings with
        {
            MinimumRangeMeters = minimum,
            MaximumRangeMeters = maximum,
            VoiceAnnouncementEnabled = _voiceAnnouncementEnabled.IsChecked == true,
            Hotkeys = _hotkeys.ToDictionary()
        };
        DialogResult = true;
    }

    private static string Format(HotkeyGesture gesture)
    {
        return HotkeyDisplayFormatter.Format(gesture);
    }

    private static string ActionName(HotkeyAction action) => action switch
    {
        HotkeyAction.RecordMortar => "记录炮位",
        HotkeyAction.RecordTarget => "记录目标",
        HotkeyAction.BeginClickSelection => "点击选点",
        HotkeyAction.BeginCalibration => "开始标定",
        HotkeyAction.ClearMeasurement => "清除结果",
        HotkeyAction.ToggleOverlay => "显示/隐藏",
        HotkeyAction.ToggleClickThrough => "鼠标穿透",
        HotkeyAction.PlayVoiceAnnouncement => "语音提示",
        HotkeyAction.Recalibrate => "重新标定",
        _ => "取消当前操作"
    };
}
