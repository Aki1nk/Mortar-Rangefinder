using System.Text.Json;
using System.Text.Json.Serialization;
using PubgMortarRanger.Configuration;
using PubgMortarRanger.Core;
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Configuration;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ReturnsCompleteDefaults_WhenFileDoesNotExist()
    {
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(121, settings.MinimumRangeMeters);
        Assert.Equal(AppSettings.CurrentSettingsVersion, settings.SettingsVersion);
        Assert.Equal(700, settings.MaximumRangeMeters);
        Assert.Equal(20, settings.HistoryLimit);
        Assert.Equal(0.94, settings.OverlayOpacity);
        Assert.Equal(1, settings.OverlayScale);
        Assert.Equal(1500, settings.MarkerHoldMilliseconds);
        Assert.True(settings.ClickThroughByDefault);
        Assert.True(settings.VoiceAnnouncementEnabled);
        Assert.Null(settings.OverlayPlacement);
        Assert.Null(settings.Calibration);
        AssertDefaultHotkeys(settings.Hotkeys);
    }

    [Fact]
    public void HotkeyModifiers_UseRequiredFlagValues()
    {
        Assert.Equal(0, (int)HotkeyModifiers.None);
        Assert.Equal(1, (int)HotkeyModifiers.Alt);
        Assert.Equal(2, (int)HotkeyModifiers.Control);
        Assert.Equal(4, (int)HotkeyModifiers.Shift);
        Assert.Equal(8, (int)HotkeyModifiers.Windows);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllValuesAndEnumDictionaryKeys()
    {
        var service = new SettingsService(_directory);
        var hotkeys = new Dictionary<HotkeyAction, HotkeyGesture>
        {
            [HotkeyAction.RecordMortar] = new(HotkeyModifiers.Alt, 0x70),
            [HotkeyAction.RecordTarget] = new(HotkeyModifiers.Control, 0x71),
            [HotkeyAction.BeginClickSelection] = new(HotkeyModifiers.Shift, 0x72),
            [HotkeyAction.BeginCalibration] = new(HotkeyModifiers.Windows, 0x73),
            [HotkeyAction.ClearMeasurement] = new(HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x74),
            [HotkeyAction.ToggleOverlay] = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x75),
            [HotkeyAction.ToggleClickThrough] = new(HotkeyModifiers.Control | HotkeyModifiers.Windows, 0x76),
            [HotkeyAction.CancelCurrent] = new(HotkeyModifiers.None, 0x1B, IsGlobal: false),
            [HotkeyAction.PlayVoiceAnnouncement] = new(HotkeyModifiers.None, 0x7B),
            [HotkeyAction.Recalibrate] = new(HotkeyModifiers.Control, 0x77)
        };
        var expected = AppSettings.CreateDefault() with
        {
            MinimumRangeMeters = 150,
            MaximumRangeMeters = 650,
            HistoryLimit = 35,
            OverlayOpacity = 0.8,
            OverlayScale = 1.25,
            MarkerHoldMilliseconds = 2500,
            ClickThroughByDefault = false,
            VoiceAnnouncementEnabled = false,
            OverlayPlacement = new WindowPlacement(12.5, 34.75, "display-a"),
            Calibration = new CalibrationProfile(
                new ScreenPoint(10, 20),
                new ScreenPoint(110, 20),
                100,
                1,
                "display-a",
                DateTimeOffset.UnixEpoch),
            Hotkeys = hotkeys
        };

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        AssertSettingsEqual(expected, actual);
    }

    [Fact]
    public async Task LoadAsync_IgnoresUnknownJsonFields()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            {
              "minimumRangeMeters": 200,
              "futureSetting": {
                "enabled": true
              }
            }
            """);
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(200, settings.MinimumRangeMeters);
        Assert.Equal(700, settings.MaximumRangeMeters);
        Assert.Empty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_BacksUpCorruptFile_AndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var settingsPath = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "{broken");
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(AppSettings.CreateDefault().MinimumRangeMeters, settings.MinimumRangeMeters);
        Assert.False(File.Exists(settingsPath));
        var backupPath = Assert.Single(
            Directory.GetFiles(_directory, "settings.corrupt-*.json"));
        Assert.Equal("{broken", await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task LoadAsync_BacksUpJsonWithInvalidValues_AndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """{"minimumRangeMeters":-1}""");
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(121, settings.MinimumRangeMeters);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_BacksUpJsonNull_AndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        var settingsPath = Path.Combine(_directory, "settings.json");
        await File.WriteAllTextAsync(settingsPath, "null");
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(121, settings.MinimumRangeMeters);
        Assert.False(File.Exists(settingsPath));
        var backupPath = Assert.Single(
            Directory.GetFiles(_directory, "settings.corrupt-*.json"));
        Assert.Equal("null", await File.ReadAllTextAsync(backupPath));
    }

    [Fact]
    public async Task LoadAsync_BacksUpNullHotkeys_AndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """{"hotkeys":null}""");
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        AssertDefaultHotkeys(settings.Hotkeys);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_BacksUpNullHotkeyGesture_AndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """{"hotkeys":{"RecordMortar":null}}""");
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        AssertDefaultHotkeys(settings.Hotkeys);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Theory]
    [InlineData(0, 0, 1, 0.25, 0.75, 250)]
    [InlineData(700, 700, 500, 1, 2, 10000)]
    public async Task SaveAsync_AcceptsValidationBoundaries(
        double minimumRangeMeters,
        double maximumRangeMeters,
        int historyLimit,
        double overlayOpacity,
        double overlayScale,
        int markerHoldMilliseconds)
    {
        var service = new SettingsService(_directory);
        var expected = AppSettings.CreateDefault() with
        {
            MinimumRangeMeters = minimumRangeMeters,
            MaximumRangeMeters = maximumRangeMeters,
            HistoryLimit = historyLimit,
            OverlayOpacity = overlayOpacity,
            OverlayScale = overlayScale,
            MarkerHoldMilliseconds = markerHoldMilliseconds
        };

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        AssertSettingsEqual(expected, actual);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task SaveAsync_RejectsInvalidMinimumRange(double value)
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { MinimumRangeMeters = value };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Theory]
    [InlineData(120)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task SaveAsync_RejectsInvalidMaximumRange(double value)
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { MaximumRangeMeters = value };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(501)]
    public async Task SaveAsync_RejectsHistoryLimitOutsideAllowedRange(int value)
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { HistoryLimit = value };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Theory]
    [InlineData(0.249)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task SaveAsync_RejectsInvalidOverlayOpacity(double value)
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { OverlayOpacity = value };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Theory]
    [InlineData(0.749)]
    [InlineData(2.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task SaveAsync_RejectsInvalidOverlayScale(double value)
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { OverlayScale = value };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Theory]
    [InlineData(249)]
    [InlineData(10001)]
    public async Task SaveAsync_RejectsMarkerHoldOutsideAllowedRange(int value)
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { MarkerHoldMilliseconds = value };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsNullHotkeys()
    {
        var service = new SettingsService(_directory);
        var settings = AppSettings.CreateDefault() with { Hotkeys = null! };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsNullHotkeyGesture()
    {
        var service = new SettingsService(_directory);
        var hotkeys = HotkeyGesture.CreateDefaults().ToDictionary();
        hotkeys[HotkeyAction.RecordMortar] = null!;
        var settings = AppSettings.CreateDefault() with { Hotkeys = hotkeys };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsNullSettingsWithClearException()
    {
        var service = new SettingsService(_directory);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.SaveAsync(null!));

        Assert.Equal("settings", exception.ParamName);
    }

    [Fact]
    public async Task SaveAsync_WhenCancelled_DoesNotDamageExistingFile()
    {
        var service = new SettingsService(_directory);
        var original = AppSettings.CreateDefault() with { MinimumRangeMeters = 200 };
        await service.SaveAsync(original);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SaveAsync(
                original with { MinimumRangeMeters = 300 },
                cancellation.Token));

        var actual = await service.LoadAsync();
        AssertSettingsEqual(original, actual);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task ConcurrentSaves_FromDifferentServices_LeaveOneCompleteCandidate()
    {
        var candidates = Enumerable.Range(1, 16)
            .Select(CreateConcurrentCandidate)
            .ToArray();

        await Task.WhenAll(candidates.Select(settings =>
            new SettingsService(_directory).SaveAsync(settings)));

        var actual = await new SettingsService(_directory).LoadAsync();

        Assert.Contains(candidates, candidate => SettingsMatch(candidate, actual));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task ConcurrentReadsAndWrites_FromDifferentServices_DoNotCorruptSettings()
    {
        var candidates = Enumerable.Range(0, 16)
            .Select(CreateConcurrentCandidate)
            .ToArray();
        await new SettingsService(_directory).SaveAsync(candidates[0]);

        var reads = Enumerable.Range(0, 48).Select(async _ =>
        {
            var actual = await new SettingsService(_directory).LoadAsync();
            Assert.Contains(candidates, candidate => SettingsMatch(candidate, actual));
        });
        var writes = candidates.Skip(1).Select(settings =>
            new SettingsService(_directory).SaveAsync(settings));

        await Task.WhenAll(reads.Concat(writes));

        var final = await new SettingsService(_directory).LoadAsync();
        Assert.Contains(candidates, candidate => SettingsMatch(candidate, final));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task ConcurrentLoads_OfOneCorruptFile_BackUpOnceAndReturnDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            "{\"futureSetting\":\"" + new string('x', 1024 * 1024) + "\"}!");
        var start = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var loads = Enumerable.Range(0, 16).Select(async _ =>
        {
            await start.Task;
            return await new SettingsService(_directory).LoadAsync();
        }).ToArray();

        start.SetResult(true);
        var settings = await Task.WhenAll(loads);

        Assert.All(settings, item => Assert.Equal(121, item.MinimumRangeMeters));
        Assert.False(File.Exists(Path.Combine(_directory, "settings.json")));
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task AtomicJsonFile_WhenSerializationFails_PreservesExistingFileAndCleansTemporaryFile()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "settings.json");
        const string originalJson = """{"value":"original"}""";
        await File.WriteAllTextAsync(path, originalJson);
        var file = new AtomicJsonFile<UnserializablePayload>(path);

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            file.WriteAsync(new UnserializablePayload()));

        Assert.Equal(originalJson, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public async Task AtomicJsonFile_WhenReplacementFails_PreservesExistingFileAndCleansTemporaryFile()
    {
        var path = Path.Combine(_directory, "settings.json");
        var file = new AtomicJsonFile<AtomicPayload>(path);
        await file.WriteAsync(new AtomicPayload { Value = "original" });

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() =>
                file.WriteAsync(new AtomicPayload { Value = "replacement" }));
        }

        var actual = await file.ReadAsync();

        Assert.NotNull(actual);
        Assert.Equal("original", actual.Value);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Theory]
    [InlineData(true, double.NaN)]
    [InlineData(true, double.PositiveInfinity)]
    [InlineData(true, double.NegativeInfinity)]
    [InlineData(false, double.NaN)]
    [InlineData(false, double.PositiveInfinity)]
    [InlineData(false, double.NegativeInfinity)]
    public async Task SaveAsync_RejectsNestedPlacementWithNonFiniteCoordinates(
        bool invalidLeft,
        double invalidCoordinate)
    {
        var service = new SettingsService(_directory);
        var placement = invalidLeft
            ? new WindowPlacement(invalidCoordinate, 10, "display-a")
            : new WindowPlacement(10, invalidCoordinate, "display-a");
        var settings = AppSettings.CreateDefault() with { OverlayPlacement = placement };

        await Assert.ThrowsAsync<JsonException>(() => service.SaveAsync(settings));
    }

    [Fact]
    public async Task LoadAsync_BacksUpNestedPlacementWithNonFiniteCoordinates()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """{"overlayPlacement":{"left":1e400,"top":0,"displayDeviceName":"display-a"}}""");
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Null(settings.OverlayPlacement);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Theory]
    [InlineData(true, true, double.NaN)]
    [InlineData(true, true, double.PositiveInfinity)]
    [InlineData(true, true, double.NegativeInfinity)]
    [InlineData(true, false, double.NaN)]
    [InlineData(true, false, double.PositiveInfinity)]
    [InlineData(true, false, double.NegativeInfinity)]
    [InlineData(false, true, double.NaN)]
    [InlineData(false, true, double.PositiveInfinity)]
    [InlineData(false, true, double.NegativeInfinity)]
    [InlineData(false, false, double.NaN)]
    [InlineData(false, false, double.PositiveInfinity)]
    [InlineData(false, false, double.NegativeInfinity)]
    public async Task SaveAsync_RejectsNestedCalibrationWithNonFinitePointCoordinates(
        bool invalidFirstPoint,
        bool invalidX,
        double invalidCoordinate)
    {
        var invalidPoint = invalidX
            ? new ScreenPoint(invalidCoordinate, 0)
            : new ScreenPoint(0, invalidCoordinate);
        var firstPoint = invalidFirstPoint ? invalidPoint : new ScreenPoint(0, 0);
        var secondPoint = invalidFirstPoint ? new ScreenPoint(100, 0) : invalidPoint;
        var settings = AppSettings.CreateDefault() with
        {
            Calibration = new CalibrationProfile(
                firstPoint,
                secondPoint,
                100,
                1,
                "display-a",
                DateTimeOffset.UnixEpoch)
        };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsNestedCalibrationWithCoincidentPoints()
    {
        var point = new ScreenPoint(10, 20);
        var settings = AppSettings.CreateDefault() with
        {
            Calibration = new CalibrationProfile(
                point,
                point,
                100,
                1,
                "display-a",
                DateTimeOffset.UnixEpoch)
        };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsNestedCalibrationWithMismatchedMetersPerPixel()
    {
        var settings = AppSettings.CreateDefault() with
        {
            Calibration = new CalibrationProfile(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                100,
                2,
                "display-a",
                DateTimeOffset.UnixEpoch)
        };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, -1)]
    [InlineData(true, double.NaN)]
    [InlineData(true, double.PositiveInfinity)]
    [InlineData(true, double.NegativeInfinity)]
    [InlineData(false, 0)]
    [InlineData(false, -1)]
    [InlineData(false, double.NaN)]
    [InlineData(false, double.PositiveInfinity)]
    [InlineData(false, double.NegativeInfinity)]
    public async Task SaveAsync_RejectsNestedCalibrationWithInvalidScales(
        bool invalidKnownMeters,
        double invalidValue)
    {
        var settings = AppSettings.CreateDefault() with
        {
            Calibration = new CalibrationProfile(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                invalidKnownMeters ? invalidValue : 100,
                invalidKnownMeters ? 1 : invalidValue,
                "display-a",
                DateTimeOffset.UnixEpoch)
        };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SaveAsync_RejectsNestedCalibrationWithBlankDisplayFingerprint(
        string displayFingerprint)
    {
        var settings = AppSettings.CreateDefault() with
        {
            Calibration = new CalibrationProfile(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                100,
                1,
                displayFingerprint,
                DateTimeOffset.UnixEpoch)
        };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Fact]
    public async Task LoadAsync_BacksUpNestedCalibrationWithCoincidentPoints()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            {
              "calibration": {
                "firstPoint": { "x": 10, "y": 20 },
                "secondPoint": { "x": 10, "y": 20 },
                "knownMeters": 100,
                "metersPerPixel": 1,
                "displayFingerprint": "display-a",
                "createdAtUtc": "1970-01-01T00:00:00+00:00"
              }
            }
            """);

        var settings = await new SettingsService(_directory).LoadAsync();

        Assert.Null(settings.Calibration);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_BacksUpNestedCalibrationWithMismatchedMetersPerPixel()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            {
              "calibration": {
                "firstPoint": { "x": 0, "y": 0 },
                "secondPoint": { "x": 100, "y": 0 },
                "knownMeters": 100,
                "metersPerPixel": 2,
                "displayFingerprint": "display-a",
                "createdAtUtc": "1970-01-01T00:00:00+00:00"
              }
            }
            """);

        var settings = await new SettingsService(_directory).LoadAsync();

        Assert.Null(settings.Calibration);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public void HotkeyStructure_CreateDefaults_ReturnsTrulyReadOnlyDictionary()
    {
        var defaults = HotkeyGesture.CreateDefaults();
        var dictionary = Assert.IsAssignableFrom<
            IDictionary<HotkeyAction, HotkeyGesture>>(defaults);

        Assert.True(dictionary.IsReadOnly);
        Assert.Throws<NotSupportedException>(() =>
            dictionary[HotkeyAction.RecordMortar] =
                new HotkeyGesture(HotkeyModifiers.Alt, 0x70));
    }

    [Fact]
    public async Task SaveAsync_RejectsHotkeyStructureWithMissingAction()
    {
        var hotkeys = HotkeyGesture.CreateDefaults().ToDictionary();
        hotkeys.Remove(HotkeyAction.RecordMortar);
        var settings = AppSettings.CreateDefault() with { Hotkeys = hotkeys };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsHotkeyStructureWithUndefinedAction()
    {
        var hotkeys = HotkeyGesture.CreateDefaults().ToDictionary();
        hotkeys[(HotkeyAction)999] = new HotkeyGesture(HotkeyModifiers.None, 0x70);
        var settings = AppSettings.CreateDefault() with { Hotkeys = hotkeys };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Fact]
    public async Task SaveAsync_RejectsHotkeyStructureWithUndefinedModifiers()
    {
        var hotkeys = HotkeyGesture.CreateDefaults().ToDictionary();
        hotkeys[HotkeyAction.RecordMortar] =
            new HotkeyGesture((HotkeyModifiers)16, 0x70);
        var settings = AppSettings.CreateDefault() with { Hotkeys = hotkeys };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public async Task SaveAsync_RejectsHotkeyStructureWithInvalidVirtualKey(
        int virtualKey)
    {
        var hotkeys = HotkeyGesture.CreateDefaults().ToDictionary();
        hotkeys[HotkeyAction.RecordMortar] =
            new HotkeyGesture(HotkeyModifiers.None, virtualKey);
        var settings = AppSettings.CreateDefault() with { Hotkeys = hotkeys };

        await Assert.ThrowsAsync<JsonException>(() =>
            new SettingsService(_directory).SaveAsync(settings));
    }

    [Fact]
    public async Task LoadAsync_BacksUpHotkeyStructureWithMissingAction()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            """
            {
              "hotkeys": {
                "RecordMortar": {
                  "modifiers": "None",
                  "virtualKey": 117,
                  "isGlobal": true
                }
              }
            }
            """);

        var settings = await new SettingsService(_directory).LoadAsync();

        AssertDefaultHotkeys(settings.Hotkeys);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_UpgradesExistingSettingsWithVoiceHotkey()
    {
        var existingHotkeys = HotkeyGesture.CreateDefaults()
            .Where(pair => pair.Key != HotkeyAction.PlayVoiceAnnouncement)
            .ToDictionary();
        var existingSettings = AppSettings.CreateDefault() with
        {
            VoiceAnnouncementEnabled = false,
            Hotkeys = existingHotkeys
        };
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            JsonSerializer.Serialize(existingSettings, serializerOptions));

        var settings = await new SettingsService(_directory).LoadAsync();

        Assert.False(settings.VoiceAnnouncementEnabled);
        Assert.Equal(
            new HotkeyGesture(HotkeyModifiers.Control, 0x7B),
            settings.Hotkeys[HotkeyAction.PlayVoiceAnnouncement]);
        Assert.Empty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_UpgradesExistingSettingsWithRecalibrateHotkey()
    {
        var existingHotkeys = HotkeyGesture.CreateDefaults()
            .Where(pair => pair.Key != HotkeyAction.Recalibrate)
            .ToDictionary();
        var existingSettings = AppSettings.CreateDefault() with
        {
            SettingsVersion = 1,
            Hotkeys = existingHotkeys
        };
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            JsonSerializer.Serialize(existingSettings, serializerOptions));

        var settings = await new SettingsService(_directory).LoadAsync();

        Assert.Equal(
            new HotkeyGesture(HotkeyModifiers.Control, 0x77),
            settings.Hotkeys[HotkeyAction.Recalibrate]);
        Assert.Empty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_UpgradesReservedDefaultF12VoiceHotkey()
    {
        var existingHotkeys = HotkeyGesture.CreateDefaults().ToDictionary();
        existingHotkeys[HotkeyAction.PlayVoiceAnnouncement] =
            new HotkeyGesture(HotkeyModifiers.None, 0x7B);
        var existingSettings = AppSettings.CreateDefault() with
        {
            SettingsVersion = 1,
            Hotkeys = existingHotkeys
        };
        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        serializerOptions.Converters.Add(
            new JsonStringEnumConverter(allowIntegerValues: false));
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            JsonSerializer.Serialize(existingSettings, serializerOptions));

        var settings = await new SettingsService(_directory).LoadAsync();

        Assert.Equal(
            new HotkeyGesture(HotkeyModifiers.Control, 0x7B),
            settings.Hotkeys[HotkeyAction.PlayVoiceAnnouncement]);
        Assert.Empty(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_BacksUpHotkeyStructureWithUndefinedAction()
    {
        Directory.CreateDirectory(_directory);
        var json = CreateCompleteHotkeysJson("\"None\"").Replace(
            "\"CancelCurrent\":",
            "\"999\":{\"modifiers\":\"None\",\"virtualKey\":112,\"isGlobal\":true},\"CancelCurrent\":",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), json);

        var settings = await new SettingsService(_directory).LoadAsync();

        AssertDefaultHotkeys(settings.Hotkeys);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [Fact]
    public async Task LoadAsync_BacksUpHotkeyStructureWithNumericEnumValue()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, "settings.json"),
            CreateCompleteHotkeysJson("0"));

        var settings = await new SettingsService(_directory).LoadAsync();

        AssertDefaultHotkeys(settings.Hotkeys);
        Assert.Single(Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static void AssertDefaultHotkeys(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> hotkeys)
    {
        var expected = new Dictionary<HotkeyAction, HotkeyGesture>
        {
            [HotkeyAction.RecordMortar] = new(HotkeyModifiers.None, 0x75),
            [HotkeyAction.RecordTarget] = new(HotkeyModifiers.None, 0x76),
            [HotkeyAction.BeginClickSelection] = new(HotkeyModifiers.None, 0x77),
            [HotkeyAction.BeginCalibration] = new(HotkeyModifiers.None, 0x78),
            [HotkeyAction.ClearMeasurement] = new(HotkeyModifiers.None, 0x79),
            [HotkeyAction.ToggleOverlay] = new(HotkeyModifiers.None, 0x7A),
            [HotkeyAction.ToggleClickThrough] = new(HotkeyModifiers.Control, 0x7A),
            [HotkeyAction.CancelCurrent] = new(HotkeyModifiers.None, 0x1B, IsGlobal: false),
            [HotkeyAction.PlayVoiceAnnouncement] = new(HotkeyModifiers.Control, 0x7B),
            [HotkeyAction.Recalibrate] = new(HotkeyModifiers.Control, 0x77)
        };

        AssertHotkeysEqual(expected, hotkeys);
    }

    private static void AssertSettingsEqual(AppSettings expected, AppSettings actual)
    {
        Assert.Equal(expected.MinimumRangeMeters, actual.MinimumRangeMeters);
        Assert.Equal(expected.SettingsVersion, actual.SettingsVersion);
        Assert.Equal(expected.MaximumRangeMeters, actual.MaximumRangeMeters);
        Assert.Equal(expected.HistoryLimit, actual.HistoryLimit);
        Assert.Equal(expected.OverlayOpacity, actual.OverlayOpacity);
        Assert.Equal(expected.OverlayScale, actual.OverlayScale);
        Assert.Equal(expected.MarkerHoldMilliseconds, actual.MarkerHoldMilliseconds);
        Assert.Equal(expected.ClickThroughByDefault, actual.ClickThroughByDefault);
        Assert.Equal(expected.VoiceAnnouncementEnabled, actual.VoiceAnnouncementEnabled);
        Assert.Equal(expected.OverlayPlacement, actual.OverlayPlacement);
        Assert.Equal(expected.Calibration, actual.Calibration);
        AssertHotkeysEqual(expected.Hotkeys, actual.Hotkeys);
    }

    private static void AssertHotkeysEqual(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> expected,
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> actual)
    {
        Assert.Equal(expected.Count, actual.Count);

        foreach (var (action, gesture) in expected)
        {
            Assert.True(actual.TryGetValue(action, out var actualGesture));
            Assert.Equal(gesture, actualGesture);
        }
    }

    private static AppSettings CreateConcurrentCandidate(int index)
    {
        return AppSettings.CreateDefault() with
        {
            MinimumRangeMeters = 100 + index,
            MaximumRangeMeters = 500 + index,
            HistoryLimit = 10 + index,
            OverlayOpacity = 0.5 + (index / 100d),
            OverlayScale = 0.9 + (index / 100d),
            MarkerHoldMilliseconds = 1000 + index,
            ClickThroughByDefault = index % 2 == 0,
            VoiceAnnouncementEnabled = index % 3 == 0,
            OverlayPlacement = new WindowPlacement(
                index,
                -index,
                $"display-{index}"),
            Calibration = new CalibrationProfile(
                new ScreenPoint(index, 0),
                new ScreenPoint(index + 10, 0),
                100 + index,
                (100 + index) / 10d,
                $"display-{index}",
                DateTimeOffset.UnixEpoch)
        };
    }

    private static bool SettingsMatch(AppSettings expected, AppSettings actual)
    {
        return expected.MinimumRangeMeters == actual.MinimumRangeMeters &&
            expected.MaximumRangeMeters == actual.MaximumRangeMeters &&
            expected.HistoryLimit == actual.HistoryLimit &&
            expected.OverlayOpacity == actual.OverlayOpacity &&
            expected.OverlayScale == actual.OverlayScale &&
            expected.MarkerHoldMilliseconds == actual.MarkerHoldMilliseconds &&
            expected.ClickThroughByDefault == actual.ClickThroughByDefault &&
            expected.VoiceAnnouncementEnabled == actual.VoiceAnnouncementEnabled &&
            expected.OverlayPlacement == actual.OverlayPlacement &&
            expected.Calibration == actual.Calibration &&
            expected.Hotkeys.Count == actual.Hotkeys.Count &&
            expected.Hotkeys.All(pair =>
                actual.Hotkeys.TryGetValue(pair.Key, out var actualGesture) &&
                actualGesture == pair.Value);
    }

    private static string CreateCompleteHotkeysJson(string modifiers)
    {
        return $$"""
            {
              "hotkeys": {
                "RecordMortar": { "modifiers": {{modifiers}}, "virtualKey": 117, "isGlobal": true },
                "RecordTarget": { "modifiers": {{modifiers}}, "virtualKey": 118, "isGlobal": true },
                "BeginClickSelection": { "modifiers": {{modifiers}}, "virtualKey": 119, "isGlobal": true },
                "BeginCalibration": { "modifiers": {{modifiers}}, "virtualKey": 120, "isGlobal": true },
                "ClearMeasurement": { "modifiers": {{modifiers}}, "virtualKey": 121, "isGlobal": true },
                "ToggleOverlay": { "modifiers": {{modifiers}}, "virtualKey": 122, "isGlobal": true },
                "ToggleClickThrough": { "modifiers": {{modifiers}}, "virtualKey": 122, "isGlobal": true },
                "CancelCurrent": { "modifiers": {{modifiers}}, "virtualKey": 27, "isGlobal": false },
                "PlayVoiceAnnouncement": { "modifiers": {{modifiers}}, "virtualKey": 123, "isGlobal": true },
                "Recalibrate": { "modifiers": {{modifiers}}, "virtualKey": 119, "isGlobal": true }
              }
            }
            """;
    }

    private sealed class AtomicPayload
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class UnserializablePayload
    {
        public Action Callback { get; } = () => { };
    }
}
