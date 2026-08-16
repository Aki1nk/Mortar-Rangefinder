# PUBG 迫击炮测距工具 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个离线 Windows WPF 悬浮工具，通过人工标定、全局热键和鼠标点击计算 PUBG 迫击炮目标距离、方位角、像素差与射程状态。

**Architecture:** 使用纯 C# 领域层实现标定、测量和状态机，Windows/WPF 层只负责全局输入、DPI/显示器坐标、透明窗口和托盘生命周期。设置与历史通过可恢复的原子 JSON 文件持久化，UI 使用轻量 MVVM，并通过接口隔离 Windows API 以便单元测试。

**Tech Stack:** C# 14、.NET 10 LTS、WPF、System.Windows.Forms `NotifyIcon`、Win32 P/Invoke、xUnit、System.Text.Json

---

## 实施前提

当前机器只有 .NET 7 Runtime，没有安装任何 .NET SDK。执行 Task 1 前必须安装 .NET 10 SDK；安装属于系统级变更，应在执行阶段获得用户确认后运行。

官方安装命令：

```powershell
winget install Microsoft.DotNet.SDK.10
```

安装后验证：

```powershell
dotnet --list-sdks
```

预期输出至少包含一个 `10.0.xxx` SDK。

当前目录不是 Git 仓库。根据上级约束，本计划不包含自动 `git init` 或提交步骤；只有用户明确要求版本控制后，才增加提交检查点。

## 文件结构

```text
PubgMortarRanger.sln
global.json
Directory.Build.props
.gitignore
README.md
src/PubgMortarRanger/
  PubgMortarRanger.csproj
  App.xaml
  App.xaml.cs
  app.manifest
  Core/
    ScreenPoint.cs
    CalibrationProfile.cs
    MeasurementResult.cs
    RangeStatus.cs
    CalibrationService.cs
    MeasurementService.cs
  Configuration/
    AppSettings.cs
    WindowPlacement.cs
    AtomicJsonFile.cs
    SettingsService.cs
  History/
    MeasurementHistoryEntry.cs
    HistoryService.cs
  Input/
    HotkeyAction.cs
    HotkeyGesture.cs
    HotkeyValidationResult.cs
    HotkeyValidator.cs
    GlobalHotkeyService.cs
    CursorPositionService.cs
  Displays/
    DisplayFingerprint.cs
    DisplayTopologyService.cs
    CoordinateTransform.cs
  Workflow/
    RangingState.cs
    RangingController.cs
  Interop/
    NativeMethods.cs
    WindowStyleService.cs
  Presentation/
    ObservableObject.cs
    RelayCommand.cs
    OverlayViewModel.cs
    SettingsViewModel.cs
  Views/
    OverlayWindow.xaml
    OverlayWindow.xaml.cs
    SelectionOverlayWindow.xaml
    SelectionOverlayWindow.xaml.cs
    SelectionOverlayManager.cs
    CalibrationDistanceWindow.xaml
    CalibrationDistanceWindow.xaml.cs
    SettingsWindow.xaml
    SettingsWindow.xaml.cs
  Lifecycle/
    SingleInstanceCoordinator.cs
    TrayIconService.cs
  Properties/PublishProfiles/
    win-x64.pubxml
tests/PubgMortarRanger.Tests/
  PubgMortarRanger.Tests.csproj
  Core/CalibrationServiceTests.cs
  Core/MeasurementServiceTests.cs
  Configuration/SettingsServiceTests.cs
  History/HistoryServiceTests.cs
  Input/HotkeyValidatorTests.cs
  Input/GlobalHotkeyServiceTests.cs
  Displays/CoordinateTransformTests.cs
  Displays/DisplayFingerprintTests.cs
  Workflow/RangingControllerTests.cs
  Presentation/OverlayViewModelTests.cs
  Presentation/SettingsViewModelTests.cs
```

---

### Task 1: 安装 SDK 并搭建可测试解决方案

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `.gitignore`
- Create: `PubgMortarRanger.sln`
- Create: `src/PubgMortarRanger/PubgMortarRanger.csproj`
- Create: `tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj`
- Create: `tests/PubgMortarRanger.Tests/ProjectSmokeTests.cs`

- [ ] **Step 1: 经用户确认后安装 .NET 10 SDK**

Run:

```powershell
winget install Microsoft.DotNet.SDK.10
```

Expected: WinGet 报告安装成功，或报告已安装兼容版本。

- [ ] **Step 2: 固定 SDK 主版本并创建基础配置**

Create `global.json`:

```json
{
  sdk: {
    version: 10.0.100,
    rollForward: latestFeature,
    allowPrerelease: false
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>14.0</LangVersion>
  </PropertyGroup>
</Project>
```

Create `.gitignore`:

```gitignore
bin/
obj/
.vs/
TestResults/
artifacts/
.superpowers/
*.user
*.suo
```

- [ ] **Step 3: 创建 WPF 应用、xUnit 测试项目和解决方案**

Run:

```powershell
dotnet new sln --format sln -n PubgMortarRanger
dotnet new wpf -n PubgMortarRanger -o src/PubgMortarRanger -f net10.0
dotnet new xunit -n PubgMortarRanger.Tests -o tests/PubgMortarRanger.Tests -f net10.0
dotnet sln PubgMortarRanger.sln add src/PubgMortarRanger/PubgMortarRanger.csproj
dotnet sln PubgMortarRanger.sln add tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj
dotnet add tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj reference src/PubgMortarRanger/PubgMortarRanger.csproj
```

Expected: 两个项目成功加入 `PubgMortarRanger.sln`。

- [ ] **Step 4: 配置 Windows 桌面能力**

Replace `src/PubgMortarRanger/PubgMortarRanger.csproj` with:

```xml
<Project Sdk=Microsoft.NET.Sdk>
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>PubgMortarRanger</AssemblyName>
    <RootNamespace>PubgMortarRanger</RootNamespace>
  </PropertyGroup>
</Project>
```

Replace the target framework in `tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj` with:

```xml
<TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

- [ ] **Step 5: 写入第一个失败烟雾测试**

Create `tests/PubgMortarRanger.Tests/ProjectSmokeTests.cs`:

```csharp
namespace PubgMortarRanger.Tests;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void ApplicationAssembly_CanBeLoaded()
    {
        var assembly = typeof(App).Assembly;

        Assert.Equal(PubgMortarRanger, assembly.GetName().Name);
    }
}
```

- [ ] **Step 6: 运行测试并修正命名空间引用**

Run:

```powershell
dotnet test PubgMortarRanger.sln
```

Expected: 初次可能因模板命名空间或 `App` 可见性失败；统一 `App.xaml.cs` 命名空间为 `PubgMortarRanger` 后全部 PASS。

---

### Task 2: 用 TDD 实现标定与测量数学

**Files:**
- Create: `src/PubgMortarRanger/Core/ScreenPoint.cs`
- Create: `src/PubgMortarRanger/Core/CalibrationProfile.cs`
- Create: `src/PubgMortarRanger/Core/MeasurementResult.cs`
- Create: `src/PubgMortarRanger/Core/RangeStatus.cs`
- Create: `src/PubgMortarRanger/Core/CalibrationService.cs`
- Create: `src/PubgMortarRanger/Core/MeasurementService.cs`
- Test: `tests/PubgMortarRanger.Tests/Core/CalibrationServiceTests.cs`
- Test: `tests/PubgMortarRanger.Tests/Core/MeasurementServiceTests.cs`

- [ ] **Step 1: 写标定失败测试**

Create `tests/PubgMortarRanger.Tests/Core/CalibrationServiceTests.cs`:

```csharp
using PubgMortarRanger.Core;

namespace PubgMortarRanger.Tests.Core;

public sealed class CalibrationServiceTests
{
    private readonly CalibrationService _service = new();

    [Fact]
    public void Create_UsesEuclideanPixelDistance()
    {
        var result = _service.Create(
            new ScreenPoint(10, 20),
            new ScreenPoint(310, 420),
            500,
            display-a);

        Assert.Equal(1, result.MetersPerPixel, 10);
        Assert.Equal(500, result.KnownMeters);
    }

    [Fact]
    public void Create_RejectsCoincidentPoints()
    {
        Assert.Throws<ArgumentException>(() =>
            _service.Create(new ScreenPoint(5, 5), new ScreenPoint(5, 5), 100, display-a));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_RejectsNonPositiveDistance(double knownMeters)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(new ScreenPoint(0, 0), new ScreenPoint(100, 0), knownMeters, display-a));
    }
}
```

- [ ] **Step 2: 运行标定测试确认失败**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter CalibrationServiceTests
```

Expected: FAIL，提示 `CalibrationService`、`ScreenPoint` 不存在。

- [ ] **Step 3: 实现最小标定模型与服务**

Create `src/PubgMortarRanger/Core/ScreenPoint.cs`:

```csharp
namespace PubgMortarRanger.Core;

public readonly record struct ScreenPoint(double X, double Y)
{
    public double DistanceTo(ScreenPoint other)
    {
        var deltaX = other.X - X;
        var deltaY = other.Y - Y;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}
```

Create `src/PubgMortarRanger/Core/CalibrationProfile.cs`:

```csharp
namespace PubgMortarRanger.Core;

public sealed record CalibrationProfile(
    ScreenPoint FirstPoint,
    ScreenPoint SecondPoint,
    double KnownMeters,
    double MetersPerPixel,
    string DisplayFingerprint,
    DateTimeOffset CreatedAtUtc);
```

Create `src/PubgMortarRanger/Core/CalibrationService.cs`:

```csharp
namespace PubgMortarRanger.Core;

public sealed class CalibrationService
{
    public CalibrationProfile Create(
        ScreenPoint firstPoint,
        ScreenPoint secondPoint,
        double knownMeters,
        string displayFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayFingerprint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(knownMeters, 0);

        var pixelDistance = firstPoint.DistanceTo(secondPoint);
        if (pixelDistance <= double.Epsilon)
        {
            throw new ArgumentException("标定点不能重合。", nameof(secondPoint));
        }

        return new CalibrationProfile(
            firstPoint,
            secondPoint,
            knownMeters,
            knownMeters / pixelDistance,
            displayFingerprint,
            DateTimeOffset.UtcNow);
    }
}
```

- [ ] **Step 4: 运行标定测试确认通过**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter CalibrationServiceTests
```

Expected: PASS。


- [ ] **Step 5: 写测量失败测试**

Create `tests/PubgMortarRanger.Tests/Core/MeasurementServiceTests.cs`:

```csharp
using PubgMortarRanger.Core;

namespace PubgMortarRanger.Tests.Core;

public sealed class MeasurementServiceTests
{
    private readonly MeasurementService _service = new();
    private readonly CalibrationProfile _calibration = new(
        new ScreenPoint(0, 0),
        new ScreenPoint(100, 0),
        100,
        1,
        display-a,
        DateTimeOffset.UnixEpoch);

    [Theory]
    [InlineData(0, -100, 100, 0)]
    [InlineData(100, 0, 100, 90)]
    [InlineData(0, 100, 100, 180)]
    [InlineData(-100, 0, 100, 270)]
    public void Measure_ReturnsDistanceAndClockwiseBearing(
        double targetX,
        double targetY,
        double expectedMeters,
        double expectedBearing)
    {
        var result = _service.Measure(
            new ScreenPoint(0, 0),
            new ScreenPoint(targetX, targetY),
            _calibration,
            121,
            700);

        Assert.Equal(expectedMeters, result.DistanceMeters, 10);
        Assert.Equal(expectedBearing, result.BearingDegrees, 10);
    }

    [Theory]
    [InlineData(120, RangeStatus.TooClose)]
    [InlineData(121, RangeStatus.InRange)]
    [InlineData(700, RangeStatus.InRange)]
    [InlineData(701, RangeStatus.TooFar)]
    public void Measure_ClassifiesRange(double distance, RangeStatus expected)
    {
        var result = _service.Measure(
            new ScreenPoint(0, 0),
            new ScreenPoint(distance, 0),
            _calibration,
            121,
            700);

        Assert.Equal(expected, result.RangeStatus);
    }
}
```

- [ ] **Step 6: 运行测量测试确认失败**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter MeasurementServiceTests
```

Expected: FAIL，提示测量类型不存在。

- [ ] **Step 7: 实现测量结果、射程状态和计算服务**

Create `src/PubgMortarRanger/Core/RangeStatus.cs`:

```csharp
namespace PubgMortarRanger.Core;

public enum RangeStatus
{
    TooClose,
    InRange,
    TooFar
}
```

Create `src/PubgMortarRanger/Core/MeasurementResult.cs`:

```csharp
namespace PubgMortarRanger.Core;

public sealed record MeasurementResult(
    ScreenPoint MortarPoint,
    ScreenPoint TargetPoint,
    double DeltaX,
    double DeltaY,
    double DistanceMeters,
    double BearingDegrees,
    RangeStatus RangeStatus,
    DateTimeOffset MeasuredAtUtc);
```

Create `src/PubgMortarRanger/Core/MeasurementService.cs`:

```csharp
namespace PubgMortarRanger.Core;

public sealed class MeasurementService
{
    public MeasurementResult Measure(
        ScreenPoint mortarPoint,
        ScreenPoint targetPoint,
        CalibrationProfile calibration,
        double minimumRangeMeters,
        double maximumRangeMeters)
    {
        ArgumentNullException.ThrowIfNull(calibration);
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRangeMeters, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumRangeMeters, minimumRangeMeters);

        var deltaX = targetPoint.X - mortarPoint.X;
        var deltaY = targetPoint.Y - mortarPoint.Y;
        var pixelDistance = mortarPoint.DistanceTo(targetPoint);
        var distanceMeters = pixelDistance * calibration.MetersPerPixel;
        var bearing = NormalizeDegrees(Math.Atan2(deltaX, -deltaY) * 180d / Math.PI);
        var rangeStatus = distanceMeters < minimumRangeMeters
            ? RangeStatus.TooClose
            : distanceMeters > maximumRangeMeters
                ? RangeStatus.TooFar
                : RangeStatus.InRange;

        return new MeasurementResult(
            mortarPoint,
            targetPoint,
            deltaX,
            deltaY,
            distanceMeters,
            bearing,
            rangeStatus,
            DateTimeOffset.UtcNow);
    }

    private static double NormalizeDegrees(double degrees) => (degrees + 360d) % 360d;
}
```

- [ ] **Step 8: 运行全部核心测试**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter FullyQualifiedName~Core
```

Expected: PASS。


---

### Task 3: 实现设置与原子 JSON 持久化

**Files:**
- Create: `src/PubgMortarRanger/Configuration/AppSettings.cs`
- Create: `src/PubgMortarRanger/Configuration/WindowPlacement.cs`
- Create: `src/PubgMortarRanger/Configuration/AtomicJsonFile.cs`
- Create: `src/PubgMortarRanger/Configuration/SettingsService.cs`
- Create: `src/PubgMortarRanger/Input/HotkeyAction.cs`
- Create: `src/PubgMortarRanger/Input/HotkeyGesture.cs`
- Test: `tests/PubgMortarRanger.Tests/Configuration/SettingsServiceTests.cs`

- [ ] **Step 1: 写默认值、往返保存和损坏恢复测试**

Create `tests/PubgMortarRanger.Tests/Configuration/SettingsServiceTests.cs`:

```csharp
using PubgMortarRanger.Configuration;

namespace PubgMortarRanger.Tests.Configuration;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(N));

    [Fact]
    public async Task LoadAsync_ReturnsDefaults_WhenFileDoesNotExist()
    {
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(121, settings.MinimumRangeMeters);
        Assert.Equal(700, settings.MaximumRangeMeters);
        Assert.Equal(20, settings.HistoryLimit);
        Assert.True(settings.ClickThroughByDefault);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsValues()
    {
        var service = new SettingsService(_directory);
        var expected = AppSettings.CreateDefault() with
        {
            MinimumRangeMeters = 150,
            MaximumRangeMeters = 650,
            OverlayOpacity = 0.8
        };

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        Assert.Equal(expected.MinimumRangeMeters, actual.MinimumRangeMeters);
        Assert.Equal(expected.MaximumRangeMeters, actual.MaximumRangeMeters);
        Assert.Equal(expected.OverlayOpacity, actual.OverlayOpacity);
    }

    [Fact]
    public async Task LoadAsync_BacksUpCorruptFile_AndReturnsDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, settings.json), {broken);
        var service = new SettingsService(_directory);

        var settings = await service.LoadAsync();

        Assert.Equal(121, settings.MinimumRangeMeters);
        Assert.Single(Directory.GetFiles(_directory, settings.corrupt-*.json));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
```

- [ ] **Step 2: 运行设置测试确认失败**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter SettingsServiceTests
```

Expected: FAIL，提示设置类型不存在。

- [ ] **Step 3: 实现热键基础类型和设置模型**

Create `src/PubgMortarRanger/Input/HotkeyAction.cs`:

```csharp
namespace PubgMortarRanger.Input;

public enum HotkeyAction
{
    RecordMortar,
    RecordTarget,
    BeginClickSelection,
    BeginCalibration,
    ClearMeasurement,
    ToggleOverlay,
    ToggleClickThrough,
    CancelCurrent
}
```

Create `src/PubgMortarRanger/Input/HotkeyGesture.cs`:

```csharp
namespace PubgMortarRanger.Input;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed record HotkeyGesture(HotkeyModifiers Modifiers, int VirtualKey, bool IsGlobal = true)
{
    public static IReadOnlyDictionary<HotkeyAction, HotkeyGesture> CreateDefaults() =>
        new Dictionary<HotkeyAction, HotkeyGesture>
        {
            [HotkeyAction.RecordMortar] = new(HotkeyModifiers.None, 0x75),
            [HotkeyAction.RecordTarget] = new(HotkeyModifiers.None, 0x76),
            [HotkeyAction.BeginClickSelection] = new(HotkeyModifiers.None, 0x77),
            [HotkeyAction.BeginCalibration] = new(HotkeyModifiers.None, 0x78),
            [HotkeyAction.ClearMeasurement] = new(HotkeyModifiers.None, 0x79),
            [HotkeyAction.ToggleOverlay] = new(HotkeyModifiers.None, 0x7A),
            [HotkeyAction.ToggleClickThrough] = new(HotkeyModifiers.Control, 0x7A),
            [HotkeyAction.CancelCurrent] = new(HotkeyModifiers.None, 0x1B, IsGlobal: false)
        };
}
```

Create `src/PubgMortarRanger/Configuration/WindowPlacement.cs`:

```csharp
namespace PubgMortarRanger.Configuration;

public sealed record WindowPlacement(double Left, double Top, string? DisplayDeviceName);
```

Create `src/PubgMortarRanger/Configuration/AppSettings.cs`:

```csharp
using PubgMortarRanger.Core;
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Configuration;

public sealed record AppSettings
{
    public double MinimumRangeMeters { get; init; } = 121;
    public double MaximumRangeMeters { get; init; } = 700;
    public int HistoryLimit { get; init; } = 20;
    public double OverlayOpacity { get; init; } = 0.94;
    public double OverlayScale { get; init; } = 1;
    public int MarkerHoldMilliseconds { get; init; } = 1500;
    public bool ClickThroughByDefault { get; init; } = true;
    public WindowPlacement? OverlayPlacement { get; init; }
    public CalibrationProfile? Calibration { get; init; }
    public IReadOnlyDictionary<HotkeyAction, HotkeyGesture> Hotkeys { get; init; } =
        HotkeyGesture.CreateDefaults();

    public static AppSettings CreateDefault() => new();
}
```

- [ ] **Step 4: 实现通用原子 JSON 文件**

Create `src/PubgMortarRanger/Configuration/AtomicJsonFile.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubgMortarRanger.Configuration;

public sealed class AtomicJsonFile<T> where T : class
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AtomicJsonFile(string path)
    {
        _path = path;
    }

    public async Task<T?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _options, cancellationToken);
    }

    public async Task WriteAsync(T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + .tmp;

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, _options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporaryPath, _path, overwrite: true);
    }
}
```

- [ ] **Step 5: 实现设置服务的验证与损坏恢复**

Create `src/PubgMortarRanger/Configuration/SettingsService.cs`:

```csharp
using System.Text.Json;

namespace PubgMortarRanger.Configuration;

public sealed class SettingsService
{
    private readonly string _directory;
    private readonly string _path;
    private readonly AtomicJsonFile<AppSettings> _file;

    public SettingsService(string directory)
    {
        _directory = directory;
        _path = Path.Combine(directory, settings.json);
        _file = new AtomicJsonFile<AppSettings>(_path);
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _file.ReadAsync(cancellationToken);
            return settings is null ? AppSettings.CreateDefault() : Validate(settings);
        }
        catch (JsonException)
        {
            Directory.CreateDirectory(_directory);
            var backup = Path.Combine(
                _directory,
                $settings.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json);
            File.Move(_path, backup, overwrite: true);
            return AppSettings.CreateDefault();
        }
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
        _file.WriteAsync(Validate(settings), cancellationToken);

    private static AppSettings Validate(AppSettings settings)
    {
        if (settings.MinimumRangeMeters < 0 ||
            settings.MaximumRangeMeters < settings.MinimumRangeMeters ||
            settings.HistoryLimit is < 1 or > 500 ||
            settings.OverlayOpacity is < 0.25 or > 1 ||
            settings.OverlayScale is < 0.75 or > 2 ||
            settings.MarkerHoldMilliseconds is < 250 or > 10000)
        {
            throw new JsonException("设置值超出允许范围。");
        }

        return settings;
    }
}
```

- [ ] **Step 6: 运行设置测试**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter SettingsServiceTests
```

Expected: PASS。

---

### Task 4: 实现测量历史持久化

**Files:**
- Create: `src/PubgMortarRanger/History/MeasurementHistoryEntry.cs`
- Create: `src/PubgMortarRanger/History/HistoryService.cs`
- Test: `tests/PubgMortarRanger.Tests/History/HistoryServiceTests.cs`

- [ ] **Step 1: 写历史裁剪和重启恢复测试**

```csharp
[Fact]
public async Task AddAsync_KeepsNewestEntriesWithinLimit()
{
    var service = new HistoryService(_directory);
    for (var index = 1; index <= 4; index++)
    {
        await service.AddAsync(CreateEntry(index), limit: 3);
    }
    Assert.Equal([4d, 3d, 2d], service.Entries.Select(item => item.DistanceMeters));
}

[Fact]
public async Task LoadAsync_RestoresSavedEntries()
{
    var writer = new HistoryService(_directory);
    await writer.AddAsync(CreateEntry(438), limit: 20);
    var reader = new HistoryService(_directory);
    await reader.LoadAsync(limit: 20);
    Assert.Equal(438, Assert.Single(reader.Entries).DistanceMeters);
}
```

Test fixture must create a unique temp directory, delete it in `Dispose`, and create entries with `RangeStatus.InRange`.

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter HistoryServiceTests
```

Expected: FAIL，提示历史类型不存在。

- [ ] **Step 3: 实现记录模型和服务**

Create `MeasurementHistoryEntry.cs`:

```csharp
public sealed record MeasurementHistoryEntry(
    DateTimeOffset MeasuredAtUtc, double DistanceMeters, double BearingDegrees,
    double DeltaX, double DeltaY, double MetersPerPixel, RangeStatus RangeStatus)
{
    public static MeasurementHistoryEntry From(MeasurementResult result, CalibrationProfile calibration) =>
        new(result.MeasuredAtUtc, result.DistanceMeters, result.BearingDegrees,
            result.DeltaX, result.DeltaY, calibration.MetersPerPixel, result.RangeStatus);
}
```

Create `HistoryService.cs` with this public contract:

```csharp
public sealed class HistoryService
{
    public IReadOnlyList<MeasurementHistoryEntry> Entries { get; }
    public Task LoadAsync(int limit, CancellationToken cancellationToken = default);
    public Task AddAsync(MeasurementHistoryEntry entry, int limit, CancellationToken cancellationToken = default);
    public Task ClearAsync(CancellationToken cancellationToken = default);
}
```

Use `AtomicJsonFile<List<MeasurementHistoryEntry>>`; insert new entries at index 0, trim with `RemoveRange(limit, count - limit)`, and persist after add or clear.

- [ ] **Step 4: 运行历史测试**

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter HistoryServiceTests
```

Expected: PASS。

---

### Task 5: 实现可回滚的全局热键注册

**Files:**
- Create: `src/PubgMortarRanger/Input/HotkeyValidationResult.cs`
- Create: `src/PubgMortarRanger/Input/HotkeyValidator.cs`
- Create: `src/PubgMortarRanger/Input/IHotkeyRegistrar.cs`
- Create: `src/PubgMortarRanger/Input/WindowsHotkeyRegistrar.cs`
- Create: `src/PubgMortarRanger/Input/GlobalHotkeyService.cs`
- Create: `src/PubgMortarRanger/Interop/NativeMethods.cs`
- Test: `tests/PubgMortarRanger.Tests/Input/HotkeyValidatorTests.cs`
- Test: `tests/PubgMortarRanger.Tests/Input/GlobalHotkeyServiceTests.cs`

- [ ] **Step 1: 写重复绑定和注册回滚测试**

Create `tests/PubgMortarRanger.Tests/Input/HotkeyValidatorTests.cs`:

```csharp
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Input;

public sealed class HotkeyValidatorTests
{
    [Fact]
    public void Validate_RejectsDuplicateGlobalGestures()
    {
        var bindings = HotkeyGesture.CreateDefaults().ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[HotkeyAction.RecordTarget] = bindings[HotkeyAction.RecordMortar];

        var result = HotkeyValidator.Validate(bindings);

        Assert.False(result.IsValid);
        Assert.Contains("重复", result.ErrorMessage);
    }

    [Fact]
    public void Validate_IgnoresLocalCancelGesture()
    {
        var bindings = HotkeyGesture.CreateDefaults().ToDictionary(pair => pair.Key, pair => pair.Value);
        bindings[HotkeyAction.CancelCurrent] = bindings[HotkeyAction.RecordMortar] with { IsGlobal = false };

        Assert.True(HotkeyValidator.Validate(bindings).IsValid);
    }
}
```

Create `tests/PubgMortarRanger.Tests/Input/GlobalHotkeyServiceTests.cs`:

```csharp
using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Input;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Apply_RestoresPreviousBindings_WhenNewRegistrationFails()
    {
        var failingGesture = new HotkeyGesture(HotkeyModifiers.Alt, 0x7B);
        var registrar = new FakeHotkeyRegistrar(failingGesture);
        var service = new GlobalHotkeyService(registrar);
        var original = HotkeyGesture.CreateDefaults();
        Assert.True(service.Apply(original).IsValid);

        var replacement = original.ToDictionary(pair => pair.Key, pair => pair.Value);
        replacement[HotkeyAction.RecordMortar] = failingGesture;

        var result = service.Apply(replacement);

        Assert.False(result.IsValid);
        Assert.Equal(original[HotkeyAction.RecordMortar], service.ActiveBindings[HotkeyAction.RecordMortar]);
    }

    private sealed class FakeHotkeyRegistrar(HotkeyGesture failingGesture) : IHotkeyRegistrar
    {
        public bool TryRegister(int id, HotkeyGesture gesture) => gesture != failingGesture;
        public void Unregister(int id) { }
    }
}
```

- [ ] **Step 2: 运行热键测试确认失败**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter "HotkeyValidatorTests|GlobalHotkeyServiceTests"
```

Expected: FAIL，提示验证器和注册服务不存在。

- [ ] **Step 3: 实现热键验证结果**

Create `src/PubgMortarRanger/Input/HotkeyValidationResult.cs`:

```csharp
namespace PubgMortarRanger.Input;

public sealed record HotkeyValidationResult(bool IsValid, string? ErrorMessage)
{
    public static HotkeyValidationResult Success { get; } = new(true, null);
    public static HotkeyValidationResult Failure(string message) => new(false, message);
}
```

Create `src/PubgMortarRanger/Input/HotkeyValidator.cs`:

```csharp
namespace PubgMortarRanger.Input;

public static class HotkeyValidator
{
    public static HotkeyValidationResult Validate(IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        var missing = Enum.GetValues<HotkeyAction>().Where(action => !bindings.ContainsKey(action)).ToArray();
        if (missing.Length > 0)
        {
            return HotkeyValidationResult.Failure($"缺少热键：{string.Join(", ", missing)}");
        }

        var duplicate = bindings.Where(pair => pair.Value.IsGlobal)
            .GroupBy(pair => (pair.Value.Modifiers, pair.Value.VirtualKey))
            .FirstOrDefault(group => group.Count() > 1);

        return duplicate is null
            ? HotkeyValidationResult.Success
            : HotkeyValidationResult.Failure("存在重复的全局热键绑定。");
    }
}
```

Create `src/PubgMortarRanger/Input/IHotkeyRegistrar.cs`:

```csharp
namespace PubgMortarRanger.Input;

public interface IHotkeyRegistrar
{
    bool TryRegister(int id, HotkeyGesture gesture);
    void Unregister(int id);
}
```

- [ ] **Step 4: 实现事务式热键应用**

Create `src/PubgMortarRanger/Input/GlobalHotkeyService.cs`:

```csharp
namespace PubgMortarRanger.Input;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly IHotkeyRegistrar _registrar;
    private Dictionary<HotkeyAction, HotkeyGesture> _activeBindings = [];

    public GlobalHotkeyService(IHotkeyRegistrar registrar) => _registrar = registrar;
    public IReadOnlyDictionary<HotkeyAction, HotkeyGesture> ActiveBindings => _activeBindings;

    public HotkeyValidationResult Apply(IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        var validation = HotkeyValidator.Validate(bindings);
        if (!validation.IsValid) return validation;

        var previous = _activeBindings.ToDictionary(pair => pair.Key, pair => pair.Value);
        UnregisterAll(_activeBindings);
        if (TryRegisterAll(bindings))
        {
            _activeBindings = bindings.ToDictionary(pair => pair.Key, pair => pair.Value);
            return HotkeyValidationResult.Success;
        }

        UnregisterAll(bindings);
        TryRegisterAll(previous);
        _activeBindings = previous;
        return HotkeyValidationResult.Failure("系统拒绝注册该热键，已恢复原设置。");
    }

    public void Dispose() => UnregisterAll(_activeBindings);

    private bool TryRegisterAll(IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        foreach (var pair in bindings.Where(pair => pair.Value.IsGlobal))
        {
            if (!_registrar.TryRegister((int)pair.Key + 1, pair.Value)) return false;
        }
        return true;
    }

    private void UnregisterAll(IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        foreach (var action in bindings.Where(pair => pair.Value.IsGlobal).Select(pair => pair.Key))
        {
            _registrar.Unregister((int)action + 1);
        }
    }
}
```

- [ ] **Step 5: 接入 Win32 `RegisterHotKey`**

Create `src/PubgMortarRanger/Interop/NativeMethods.cs`:

```csharp
using System.Runtime.InteropServices;

namespace PubgMortarRanger.Interop;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint windowHandle, int id);
}
```

Create `src/PubgMortarRanger/Input/WindowsHotkeyRegistrar.cs`:

```csharp
using PubgMortarRanger.Interop;

namespace PubgMortarRanger.Input;

public sealed class WindowsHotkeyRegistrar(nint windowHandle) : IHotkeyRegistrar
{
    public bool TryRegister(int id, HotkeyGesture gesture) =>
        NativeMethods.RegisterHotKey(windowHandle, id, (uint)gesture.Modifiers, (uint)gesture.VirtualKey);

    public void Unregister(int id) => NativeMethods.UnregisterHotKey(windowHandle, id);
}
```

Task 10 在应用组合完成时添加 `HwndSource` 的 `WM_HOTKEY` 分发钩子。

- [ ] **Step 6: 运行热键测试**

Run:

```powershell
dotnet test tests/PubgMortarRanger.Tests/PubgMortarRanger.Tests.csproj --filter "HotkeyValidatorTests|GlobalHotkeyServiceTests"
```

Expected: PASS。

---

### Task 6: 实现显示器指纹、物理坐标和 DPI 转换

**Files:**
- Create: `src/PubgMortarRanger/Displays/DisplayFingerprint.cs`
- Create: `src/PubgMortarRanger/Displays/CoordinateTransform.cs`
- Create: `src/PubgMortarRanger/Displays/DisplayTopologyService.cs`
- Create: `src/PubgMortarRanger/Input/CursorPositionService.cs`
- Create: `src/PubgMortarRanger/app.manifest`
- Modify: `src/PubgMortarRanger/Interop/NativeMethods.cs`
- Test: `tests/PubgMortarRanger.Tests/Displays/CoordinateTransformTests.cs`
- Test: `tests/PubgMortarRanger.Tests/Displays/DisplayFingerprintTests.cs`

- [ ] **Step 1: 写负坐标、多 DPI 和指纹变化测试**

```csharp
[Fact]
public void PhysicalToDip_HandlesNegativeOriginAndScaling()
{
    var result = CoordinateTransform.PhysicalToDip(
        new ScreenPoint(-1280, 300), new ScreenPoint(-1920, 0), 1.25, 1.5);
    Assert.Equal(512, result.X, 10);
    Assert.Equal(200, result.Y, 10);
}

[Fact]
public void StableValue_ChangesWhenDpiChanges()
{
    var original = new DisplayFingerprint("DISPLAY1", 0, 0, 1920, 1080, 96, 96);
    Assert.NotEqual(original.StableValue, (original with { DpiX = 120, DpiY = 120 }).StableValue);
}
```

Run `dotnet test ... --filter "CoordinateTransformTests|DisplayFingerprintTests"`; expected FAIL。

- [ ] **Step 2: 实现纯坐标与指纹类型**

```csharp
public static class CoordinateTransform
{
    public static ScreenPoint PhysicalToDip(ScreenPoint point, ScreenPoint origin, double scaleX, double scaleY) =>
        new((point.X - origin.X) / scaleX, (point.Y - origin.Y) / scaleY);

    public static ScreenPoint DipToPhysical(ScreenPoint point, ScreenPoint origin, double scaleX, double scaleY) =>
        new(origin.X + point.X * scaleX, origin.Y + point.Y * scaleY);
}

public sealed record DisplayFingerprint(
    string DeviceName, int Left, int Top, int Width, int Height, uint DpiX, uint DpiY)
{
    public string StableValue => $"{DeviceName}|{Left},{Top},{Width},{Height}|{DpiX}x{DpiY}";
}
```

- [ ] **Step 3: 启用 Per-Monitor V2 并读取物理鼠标坐标**

`app.manifest` 的 `<windowsSettings>` 同时声明 `dpiAwareness=PerMonitorV2` 和 `dpiAware=true/pm`。在 `NativeMethods` 添加 `GetCursorPos`；`CursorPositionService.GetPhysicalPosition()` 返回 `ScreenPoint`，API 失败时抛出中文 `InvalidOperationException`。

- [ ] **Step 4: 实现显示器拓扑服务**

```csharp
public sealed class DisplayTopologyService
{
    public IReadOnlyList<DisplayFingerprint> Capture();
    public DisplayFingerprint FindForPoint(ScreenPoint physicalPoint);
    public string CaptureStableValueForPoint(ScreenPoint physicalPoint);
}
```

Use `Screen.AllScreens`, `MonitorFromPoint`, and `GetDpiForMonitor`. `CaptureStableValueForPoint` combines the active display fingerprint with all fingerprints sorted by device name, so display, resolution, DPI, or topology changes invalidate the calibration.

- [ ] **Step 5: 增加标定有效性判断并运行测试**

Add `CalibrationService.IsValidFor(profile, fingerprint)` using ordinal equality. Run `dotnet test ... --filter "Displays|CalibrationServiceTests"`; expected PASS。

---

### Task 7: 用 TDD 实现测量工作流状态机

**Files:** `Workflow/RangingState.cs`, `Workflow/RangingController.cs`, `tests/.../Workflow/RangingControllerTests.cs`

- [ ] **Step 1: 写状态转换测试**

Tests must cover: two calibration points then known distance produces `Ready`; F6-style mortar then F7-style target produces a measurement; click mode requests mortar then target; target before mortar throws; cancel restores `Ready` or `Uncalibrated`; clear removes the last result.

```csharp
[Fact]
public void ClickFlow_RequestsMortarThenTarget()
{
    var controller = CreateCalibratedController();
    controller.BeginClickMeasurement();
    controller.RecordPoint(new ScreenPoint(0, 0));
    var result = controller.RecordPoint(new ScreenPoint(100, 0));
    Assert.Equal(90, result!.BearingDegrees, 10);
    Assert.Equal(RangingState.ShowingResult, controller.State);
}
```

Run `dotnet test ... --filter RangingControllerTests`; expected FAIL。

- [ ] **Step 2: 实现状态机**

`RangingState` values: `Uncalibrated`, `Ready`, `AwaitingCalibrationFirstPoint`, `AwaitingCalibrationSecondPoint`, `AwaitingCalibrationDistance`, `AwaitingMortarPoint`, `AwaitingTargetPoint`, `ShowingResult`。

`RangingController` public API:

```csharp
public sealed class RangingController
{
    public RangingState State { get; }
    public CalibrationProfile? Calibration { get; }
    public ScreenPoint? PendingMortarPoint { get; }
    public MeasurementResult? LastMeasurement { get; }
    public event EventHandler? Changed;
    public void SetCalibration(CalibrationProfile? calibration);
    public void BeginCalibration();
    public void CompleteCalibration(double knownMeters, string displayFingerprint);
    public void BeginClickMeasurement();
    public void RecordMortar(ScreenPoint point);
    public MeasurementResult RecordTarget(ScreenPoint point);
    public MeasurementResult? RecordPoint(ScreenPoint point);
    public void UpdateRangeLimits(double minimumRangeMeters, double maximumRangeMeters);
    public void ClearMeasurement();
    public void Cancel();
}
```

Every state change raises `Changed`; invalid transitions throw `InvalidOperationException` with a user-readable Chinese message.
`UpdateRangeLimits` validates the new bounds, updates mutable range fields, recalculates `LastMeasurement` when one exists, and raises `Changed` so the current result immediately reflects settings changes.

- [ ] **Step 3: 运行状态机与核心测试**

Run `dotnet test ... --filter "RangingControllerTests|FullyQualifiedName~Core"`; expected PASS。

---

### Task 8: 构建紧凑横条和可测试 ViewModel

**Files:** `Presentation/ObservableObject.cs`, `Presentation/RelayCommand.cs`, `Presentation/OverlayViewModel.cs`, `Views/OverlayWindow.xaml`, `Views/OverlayWindow.xaml.cs`, `Interop/WindowStyleService.cs`, `tests/.../Presentation/OverlayViewModelTests.cs`

- [ ] **Step 1: 写 ViewModel 格式化测试**

Verify `438.4m` renders `438 m`, `71.6°` renders `072°`, statuses map to `过近/射程内/过远`, uncalibrated text is `未标定`, and `IsExpanded` toggles.

- [ ] **Step 2: 实现 ViewModel 基础设施和映射**

```csharp
public sealed class OverlayViewModel : ObservableObject
{
    public string DistanceText { get; private set; } = "--- m";
    public string BearingText { get; private set; } = "---°";
    public string RangeText { get; private set; } = "未标定";
    public string HintText { get; private set; } = "按 F9 开始标定";
    public bool IsExpanded { get; set; }
    public void Update(RangingController controller, AppSettings settings);
}
```

`Update` is the only formatter for controller state; XAML must not duplicate business rules.

- [ ] **Step 3: 实现确认的紧凑横条 XAML**

Use a border `#D90A1016`, green accent `#B9E46E`, width about `520`, rounded radius `10`, three columns for distance/bearing/status, a thin footer for hints, and an expandable details row for delta, scale, calibration time, and history. Set `WindowStyle=None`, `AllowsTransparency=True`, `Topmost=True`, `ShowInTaskbar=False`.

- [ ] **Step 4: 实现拖动、位置保存和鼠标穿透**

`WindowStyleService.SetClickThrough(hwnd, enabled)` toggles `WS_EX_TRANSPARENT | WS_EX_LAYERED` with `GetWindowLongPtr/SetWindowLongPtr`. When unlocked, dragging calls `DragMove()` and saves `WindowPlacement`; when display layout changes, clamp the window rectangle into a visible working area.

- [ ] **Step 5: 运行 ViewModel 测试和构建**

Run `dotnet test ... --filter OverlayViewModelTests` then `dotnet build PubgMortarRanger.sln`; expected PASS。

---

### Task 9: 实现跨显示器选择层和混合选点

**Files:** `Views/SelectionOverlayWindow.xaml`, `Views/SelectionOverlayWindow.xaml.cs`, `Views/SelectionOverlayManager.cs`, `Views/CalibrationDistanceWindow.xaml`, `Views/CalibrationDistanceWindow.xaml.cs`

- [ ] **Step 1: 写坐标转换集成测试**

Given a monitor physical origin, WPF DPI scale, and local DIP click, assert `CoordinateTransform.DipToPhysical` returns the exact virtual-desktop point passed to `RangingController`.

- [ ] **Step 2: 实现每显示器一个透明选择窗口**

`SelectionOverlayManager.Show(mode)` creates one `SelectionOverlayWindow` per `Screen.AllScreens`; this is the explicit 多显示器 implementation. Each window stores physical bounds and DPI scale, covers only its monitor, draws a crosshair, and emits physical points. Blue markers represent calibration, green mortar, red target, and a dashed line joins selected points.

- [ ] **Step 3: 实现点击与热键混合输入**

Mouse clicks call `RangingController.RecordPoint`. While calibrating, the configured F6/F7 actions record the first and second calibration points through `RecordPoint`; while measuring, they call `RecordMortar` and `RecordTarget`. All hotkey paths read the current physical cursor position. Local cancel closes all selector windows, calls `Cancel`, and always restores click-through in a `finally` block.

- [ ] **Step 4: 实现标定距离窗口**

Provide buttons `100`, `500`, `1000`, a numeric custom field, and confirm/cancel. Accept only finite values greater than zero; confirm calls `CompleteCalibration(value, displayFingerprint)` and persists settings.

- [ ] **Step 5: 手动验证选择层**

Run the app on one monitor and then two monitors: click at all corners, cancel midway, switch DPI, and verify no invisible window continues intercepting input.

---

### Task 10: 实现设置窗口和热键编辑

**Files:** `Presentation/SettingsViewModel.cs`, `Views/SettingsWindow.xaml`, `Views/SettingsWindow.xaml.cs`, `tests/.../Presentation/SettingsViewModelTests.cs`

- [ ] **Step 1: 写保存事务测试**

Tests verify duplicate bindings are rejected, failed OS registration keeps old settings, valid changes update settings and registrar, and range/opacity/history limits reject out-of-range values.

- [ ] **Step 2: 实现 ViewModel 保存事务**

```csharp
public async Task<HotkeyValidationResult> SaveAsync()
{
    var candidate = BuildCandidateSettings();
    var result = _globalHotkeys.Apply(candidate.Hotkeys);
    if (!result.IsValid) return result;
    await _settingsService.SaveAsync(candidate);
    _rangingController.UpdateRangeLimits(candidate.MinimumRangeMeters, candidate.MaximumRangeMeters);
    return HotkeyValidationResult.Success;
}
```

If persistence fails after registration, reapply old bindings and surface the exception as a nonblocking error banner.

- [ ] **Step 3: 实现设置 XAML**

Create grouped editors for all eight actions, min/max range, opacity, scale, history limit, marker duration, and click-through default. A focused hotkey field captures modifiers plus one non-modifier key; Escape clears capture instead of closing the app.

- [ ] **Step 4: 运行设置测试和手动热键冲突检查**

Run `dotnet test ... --filter SettingsViewModelTests`; then bind two actions to the same gesture and bind a known occupied OS shortcut. Expected: duplicate and registration failures are shown without losing old bindings.

---

### Task 11: 组合应用生命周期、托盘和单实例

**Files:**
- Modify: `src/PubgMortarRanger/App.xaml`
- Modify: `src/PubgMortarRanger/App.xaml.cs`
- Create: `src/PubgMortarRanger/Lifecycle/SingleInstanceCoordinator.cs`
- Create: `src/PubgMortarRanger/Lifecycle/TrayIconService.cs`
- Test: `tests/PubgMortarRanger.Tests/Lifecycle/SingleInstanceCoordinatorTests.cs`

- [ ] **Step 1: 写单实例唤醒测试**

Use a unique test name. The first coordinator must report primary; a second coordinator with the same name must report secondary and signal the first coordinator event within two seconds.

```csharp
[Fact]
public async Task SecondInstance_SignalsPrimaryInstance()
{
    var name = "PubgMortarRanger.Tests." + Guid.NewGuid().ToString("N");
    await using var first = await SingleInstanceCoordinator.CreateAsync(name);
    var signaled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    first.ActivationRequested += (_, _) => signaled.SetResult(true);
    await using var second = await SingleInstanceCoordinator.CreateAsync(name);
    Assert.False(second.IsPrimary);
    await signaled.Task.WaitAsync(TimeSpan.FromSeconds(2));
}
```

- [ ] **Step 2: 实现命名互斥量与命名管道唤醒**

`SingleInstanceCoordinator.CreateAsync(name)` owns a named `Mutex` when primary, starts a cancellation-aware `NamedPipeServerStream`, and sends one-byte activation messages from secondary instances. Disposal cancels the server and releases the mutex.

- [ ] **Step 3: 实现托盘服务**

`TrayIconService` wraps `System.Windows.Forms.NotifyIcon`. Menu items call delegates for show/hide, begin measurement, recalibrate, settings, and exit. Double-click shows the overlay. Dispose removes the icon immediately.

- [ ] **Step 4: 在 `App.xaml.cs` 组合全部模块**

Startup order:

1. Create single-instance coordinator; secondary instance signals and shuts down.
2. Resolve `%LocalAppData%/PubgMortarRanger`.
3. Load settings and history.
4. Create controller and invalidate saved calibration if display fingerprint differs.
5. Create/show `OverlayWindow` and obtain HWND in `SourceInitialized`.
6. Create `WindowsHotkeyRegistrar`, apply saved bindings, and add `HwndSource` hook for `WM_HOTKEY`.
7. Route hotkey IDs to controller, selection manager, visibility, and click-through actions.
8. Subscribe to completed measurements, append `MeasurementHistoryEntry`, refresh the overlay, and persist history.
9. Create tray service and listen for single-instance activation.

`OnExit` must dispose selection windows, hotkeys, tray, single-instance coordinator, and save final settings/history.

- [ ] **Step 5: 运行生命周期测试和启动烟雾检查**

Run `dotnet test ... --filter SingleInstanceCoordinatorTests`; start the app twice and verify only one tray icon exists and the first overlay becomes visible.

---

### Task 12: 发布、自检和验收

**Files:**
- Create: `src/PubgMortarRanger/Properties/PublishProfiles/win-x64.pubxml`
- Create: `README.md`
- Modify: `.gitignore`

- [ ] **Step 1: 创建自包含单文件发布配置**

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <PublishTrimmed>false</PublishTrimmed>
    <DebugType>none</DebugType>
    <DebugSymbols>false</DebugSymbols>
    <PublishDir>$(MSBuildProjectDirectory)\..\..\artifacts\win-x64\</PublishDir>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: 编写用户文档**

`README.md` must include: install/run, first calibration, hotkey mode, click mode, changing hotkeys, map-zoom recalibration warning, data path, default 121–700m range and how to change it, 多显示器/DPI behavior, privacy boundary, troubleshooting for occupied hotkeys and invisible click-blocking overlays. The privacy section must explicitly state: 不读取游戏进程或内存、不注入游戏、不自动控制鼠标、不访问网络。

- [ ] **Step 3: 运行自动验证**

```powershell
dotnet format PubgMortarRanger.sln --verify-no-changes
dotnet test PubgMortarRanger.sln --configuration Release
dotnet build PubgMortarRanger.sln --configuration Release --no-restore
dotnet publish src/PubgMortarRanger/PubgMortarRanger.csproj --configuration Release -p:PublishProfile=win-x64
```

Expected: formatter exit 0, all tests PASS, build has zero warnings/errors, and `artifacts/win-x64/PubgMortarRanger.exe` exists.

- [ ] **Step 4: 执行桌面验收矩阵**

Verify each item and record pass/fail in the task log:

1. Fresh launch shows `未标定` and does not block the game window.
2. Calibrate using 100m preset and custom value.
3. F6/F7 produces the same result as two-click mode for identical points.
4. Cardinal directions render `000°/090°/180°/270°`.
5. 120m, 121m, 700m, and 701m render correct range status.
6. Every hotkey can be changed, saved, restored, and rejected on collision.
7. Escape cancels active selection and restores click-through.
8. History persists and trims to the configured limit.
9. Resolution, DPI, or display topology changes invalidate calibration.
10. Second launch wakes the existing window.
11. Closing minimizes to tray; tray Exit fully terminates the process.
12. Release EXE runs on a Windows machine without a separately installed .NET SDK.

- [ ] **Step 5: 检查安全与范围边界**

Use Process Explorer or equivalent local inspection to confirm the application opens no PUBG process handle, injects no module, creates no network connection, and runs without elevation. Search source with:

```powershell
rg -n "OpenProcess|ReadProcessMemory|WriteProcessMemory|CreateRemoteThread|HttpClient|Socket|WebRequest" src
```

Expected: no prohibited API usage. Any match must be explained and removed unless it is a false positive in documentation.
Also inspect the running process and firewall/network activity; expected: no network connection and no elevated token.

- [ ] **Step 6: 最终交付检查**

Confirm `README.md`, design spec, implementation plan, test output, and `artifacts/win-x64/PubgMortarRanger.exe` are present. Remove temporary screenshots, logs, and `.superpowers/brainstorm` artifacts from release output; do not remove the approved design and plan documents.
