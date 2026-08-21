using System.IO;
using System.Text.Json;
using PubgMortarRanger.Core;
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Configuration;

public sealed class SettingsService
{
    private const string FileName = "settings.json";
    private const HotkeyModifiers DefinedHotkeyModifiers =
        HotkeyModifiers.Alt |
        HotkeyModifiers.Control |
        HotkeyModifiers.Shift |
        HotkeyModifiers.Windows;

    private static readonly HotkeyAction[] DefinedHotkeyActions =
        Enum.GetValues<HotkeyAction>();

    private readonly string _directory;
    private readonly string _path;
    private readonly AtomicJsonFile<AppSettings> _file;

    public SettingsService(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        _path = Path.Combine(_directory, FileName);
        _file = new AtomicJsonFile<AppSettings>(_path);
    }

    public async Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        return await _file.ExecuteExclusiveAsync(async token =>
        {
            try
            {
                var settings = await _file.ReadCoreAsync(token);
                if (settings is null)
                {
                    if (File.Exists(_path))
                    {
                        throw new JsonException(
                            "The settings file must contain a JSON object.");
                    }

                    return AppSettings.CreateDefault();
                }

                return Validate(Upgrade(settings));
            }
            catch (JsonException)
            {
                BackUpCorruptFile();
                return AppSettings.CreateDefault();
            }
        }, cancellationToken);
    }

    public Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return _file.WriteAsync(Validate(settings), cancellationToken);
    }

    private static AppSettings Validate(AppSettings settings)
    {
        if (settings.SettingsVersion != AppSettings.CurrentSettingsVersion)
        {
            throw new JsonException("SettingsVersion is unsupported.");
        }

        ValidateHotkeys(settings.Hotkeys);
        ValidatePlacement(settings.OverlayPlacement);
        ValidateCalibration(settings.Calibration);

        if (!double.IsFinite(settings.MinimumRangeMeters) ||
            settings.MinimumRangeMeters < 0)
        {
            throw new JsonException("MinimumRangeMeters must be finite and non-negative.");
        }

        if (!double.IsFinite(settings.MaximumRangeMeters) ||
            settings.MaximumRangeMeters < settings.MinimumRangeMeters)
        {
            throw new JsonException(
                "MaximumRangeMeters must be finite and at least MinimumRangeMeters.");
        }

        if (settings.HistoryLimit is < 1 or > 500)
        {
            throw new JsonException("HistoryLimit must be between 1 and 500.");
        }

        if (!double.IsFinite(settings.OverlayOpacity) ||
            settings.OverlayOpacity is < 0.25 or > 1)
        {
            throw new JsonException("OverlayOpacity must be between 0.25 and 1.");
        }

        if (!double.IsFinite(settings.OverlayScale) ||
            settings.OverlayScale is < 0.75 or > 2)
        {
            throw new JsonException("OverlayScale must be between 0.75 and 2.");
        }

        if (settings.MarkerHoldMilliseconds is < 250 or > 10000)
        {
            throw new JsonException(
                "MarkerHoldMilliseconds must be between 250 and 10000.");
        }

        return settings;
    }

    private static AppSettings Upgrade(AppSettings settings)
    {
        if (settings.Hotkeys is null ||
            settings.Hotkeys.Keys.Any(action => !Enum.IsDefined(action)))
        {
            return settings;
        }

        var missingActions = DefinedHotkeyActions
            .Where(action => !settings.Hotkeys.ContainsKey(action))
            .ToArray();

        var upgradeableActions = new[]
        {
            HotkeyAction.PlayVoiceAnnouncement,
            HotkeyAction.Recalibrate
        };
        if (missingActions.Any(action => !upgradeableActions.Contains(action)) ||
            settings.Hotkeys.Count + missingActions.Length != DefinedHotkeyActions.Length)
        {
            return settings;
        }

        var hotkeys = settings.Hotkeys.ToDictionary();
        foreach (var action in missingActions)
        {
            hotkeys[action] = HotkeyGesture.CreateDefaults()[action];
        }

        var oldReservedVoiceHotkey = new HotkeyGesture(HotkeyModifiers.None, 0x7B);
        if (settings.SettingsVersion < AppSettings.CurrentSettingsVersion &&
            hotkeys.TryGetValue(
                HotkeyAction.PlayVoiceAnnouncement,
                out var voiceHotkey) &&
            voiceHotkey == oldReservedVoiceHotkey)
        {
            hotkeys[HotkeyAction.PlayVoiceAnnouncement] =
                HotkeyGesture.CreateDefaults()[HotkeyAction.PlayVoiceAnnouncement];
        }

        return settings with
        {
            SettingsVersion = AppSettings.CurrentSettingsVersion,
            Hotkeys = hotkeys
        };
    }

    private void BackUpCorruptFile()
    {
        Directory.CreateDirectory(_directory);

        if (!File.Exists(_path))
        {
            return;
        }

        var backupPath = Path.Combine(
            _directory,
            $"settings.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfffffff}-{Guid.NewGuid():N}.json");

        try
        {
            File.Move(_path, backupPath);
        }
        catch (FileNotFoundException)
        {
        }
    }

    private static void ValidateHotkeys(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture>? hotkeys)
    {
        if (hotkeys is null ||
            hotkeys.Count != DefinedHotkeyActions.Length ||
            DefinedHotkeyActions.Any(action => !hotkeys.ContainsKey(action)) ||
            hotkeys.Keys.Any(action => !Enum.IsDefined(action)))
        {
            throw new JsonException("Hotkeys must contain exactly the defined actions.");
        }

        foreach (var gesture in hotkeys.Values)
        {
            if (gesture is null ||
                (gesture.Modifiers & ~DefinedHotkeyModifiers) != HotkeyModifiers.None ||
                gesture.VirtualKey is < 1 or > 255)
            {
                throw new JsonException("Hotkey gesture is invalid.");
            }
        }
    }

    private static void ValidatePlacement(WindowPlacement? placement)
    {
        if (placement is not null &&
            (!double.IsFinite(placement.Left) || !double.IsFinite(placement.Top)))
        {
            throw new JsonException("Overlay placement coordinates must be finite.");
        }
    }

    private static void ValidateCalibration(CalibrationProfile? calibration)
    {
        if (calibration is null)
        {
            return;
        }

        if (!IsFinite(calibration.FirstPoint) ||
            !IsFinite(calibration.SecondPoint) ||
            calibration.FirstPoint == calibration.SecondPoint ||
            !double.IsFinite(calibration.KnownMeters) ||
            calibration.KnownMeters <= 0 ||
            !double.IsFinite(calibration.MetersPerPixel) ||
            calibration.MetersPerPixel <= 0 ||
            string.IsNullOrWhiteSpace(calibration.DisplayFingerprint))
        {
            throw new JsonException("Calibration is invalid.");
        }

        var expectedMetersPerPixel =
            calibration.KnownMeters /
            calibration.FirstPoint.DistanceTo(calibration.SecondPoint);
        if (calibration.MetersPerPixel != expectedMetersPerPixel)
        {
            throw new JsonException("Calibration scale does not match its points.");
        }
    }

    private static bool IsFinite(ScreenPoint point) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y);
}
