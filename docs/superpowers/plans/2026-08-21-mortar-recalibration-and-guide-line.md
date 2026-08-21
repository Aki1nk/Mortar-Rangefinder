# Mortar Recalibration and Guide Line Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a configurable recalibration hotkey and a non-interactive yellow dashed line connecting the selected points across one or more monitors.

**Architecture:** Add a `Recalibrate` action to the existing hotkey/settings pipeline. Expose a nullable guide segment from `RangingController`; the app synchronizes it to a dedicated transparent guide window managed by `SelectionOverlayManager`. The guide window spans the Windows virtual desktop and is hit-test disabled, while existing selection windows continue to receive clicks.

**Tech Stack:** C#, .NET 10, WPF, xUnit, Windows virtual-screen coordinates.

---

### Task 1: Extend the hotkey model and settings migration

**Files:**
- Modify: `src/PubgMortarRanger/Input/HotkeyAction.cs`
- Modify: `src/PubgMortarRanger/Input/HotkeyGesture.cs`
- Modify: `src/PubgMortarRanger/Configuration/SettingsService.cs`
- Modify: `src/PubgMortarRanger/SettingsWindow.cs`
- Modify: `tests/PubgMortarRanger.Tests/Voice/VoiceAnnouncementFeatureTests.cs`
- Modify: `tests/PubgMortarRanger.Tests/Configuration/SettingsServiceTests.cs`

- [ ] **Step 1: Add a failing default-hotkey test**

Add a test asserting that `HotkeyAction.Recalibrate` exists and defaults to `Ctrl+F8`:

```csharp
[Fact]
public void Defaults_IncludeRecalibrateHotkey()
{
    Assert.True(Enum.TryParse<HotkeyAction>("Recalibrate", out var action));
    Assert.Equal(
        new HotkeyGesture(HotkeyModifiers.Control, 0x77),
        AppSettings.CreateDefault().Hotkeys[action]);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
& .\.dotnet\dotnet.exe test .\tests\PubgMortarRanger.Tests\PubgMortarRanger.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Defaults_IncludeRecalibrateHotkey"
```

Expected: the test fails because `Recalibrate` is not defined and the default binding is absent.

- [ ] **Step 3: Add the action and default binding**

Add `Recalibrate` to `HotkeyAction` and add:

```csharp
[HotkeyAction.Recalibrate] = new(HotkeyModifiers.Control, 0x77)
```

to `HotkeyGesture.CreateDefaults()`. Keep `BeginClickSelection` at `F8`; the modifier makes the new action distinct.

- [ ] **Step 4: Update settings upgrade and labels**

Change `SettingsService.Upgrade` to recognize the new action when upgrading older settings. If a settings file contains all pre-feature actions but lacks `Recalibrate`, copy the existing bindings and add the default recalibration gesture. Update the action label switch in `SettingsWindow` with `"重新标定"`.

- [ ] **Step 5: Run migration and hotkey tests**

Run:

```powershell
& .\.dotnet\dotnet.exe test .\tests\PubgMortarRanger.Tests\PubgMortarRanger.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SettingsServiceTests|FullyQualifiedName~VoiceAnnouncementFeatureTests"
```

Expected: all selected tests pass.

### Task 2: Add recalibration controller behavior and guide-segment state

**Files:**
- Modify: `src/PubgMortarRanger/Workflow/RangingController.cs`
- Modify: `src/PubgMortarRanger/Workflow/RangingState.cs` only if a state helper is needed
- Modify: `tests/PubgMortarRanger.Tests/Workflow/RangingControllerTests.cs`

- [ ] **Step 1: Add failing recalibration and guide-line tests**

Add tests for the intended public behavior:

```csharp
[Fact]
public void BeginCalibration_ClearsExistingMeasurementAndGuideLine()
{
    var controller = CreateCalibratedController();
    controller.RecordMortar(new ScreenPoint(0, 0));
    controller.RecordTarget(new ScreenPoint(100, 0));
    Assert.NotNull(controller.LastMeasurement);

    controller.BeginCalibration();

    Assert.Null(controller.LastMeasurement);
    Assert.Null(controller.GuideSegment);
    Assert.Equal(RangingState.AwaitingCalibrationFirstPoint, controller.State);
}

[Fact]
public void CompletedPointPairs_ExposeGuideSegment()
{
    var controller = CreateCalibratedController();
    controller.BeginClickMeasurement();
    controller.RecordPoint(new ScreenPoint(10, 20));
    controller.RecordPoint(new ScreenPoint(110, 120));

    Assert.Equal(
        new GuideSegment(new ScreenPoint(10, 20), new ScreenPoint(110, 120)),
        controller.GuideSegment);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
& .\.dotnet\dotnet.exe test .\tests\PubgMortarRanger.Tests\PubgMortarRanger.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BeginCalibration_ClearsExistingMeasurementAndGuideLine|FullyQualifiedName~CompletedPointPairs_ExposeGuideSegment"
```

Expected: compilation/test failure because `GuideSegment` is not exposed and recalibration does not clear `LastMeasurement`.

- [ ] **Step 3: Implement the guide segment value and controller state**

Create a small immutable `GuideSegment` record in `src/PubgMortarRanger/Core/GuideSegment.cs`:

```csharp
namespace PubgMortarRanger.Core;

public sealed record GuideSegment(ScreenPoint Start, ScreenPoint End);
```

Add `public GuideSegment? GuideSegment { get; private set; }` to `RangingController`. Set it only after a complete pair is captured, clear it in `BeginCalibration`, `BeginClickMeasurement`, `ClearMeasurement`, `Cancel`, `SetCalibration`, and `ResetTransientState` where appropriate, and raise `Changed` whenever it changes.

- [ ] **Step 4: Preserve the pair for each completed flow**

When the second calibration point is recorded, assign a `GuideSegment` from the two pending calibration points. When `RecordTarget` completes, assign a segment from the mortar point and target point. Ensure the segment remains available after the selection overlay closes so it can be rendered beside the result/prompt.

- [ ] **Step 5: Run controller tests**

Run:

```powershell
& .\.dotnet\dotnet.exe test .\tests\PubgMortarRanger.Tests\PubgMortarRanger.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~RangingControllerTests"
```

Expected: all controller tests pass.

### Task 3: Implement the cross-monitor dashed guide overlay

**Files:**
- Create: `src/PubgMortarRanger/GuideOverlayWindow.cs`
- Modify: `src/PubgMortarRanger/SelectionOverlayManager.cs`
- Modify: `src/PubgMortarRanger/App.xaml.cs`

- [ ] **Step 1: Add the guide window**

Create a borderless transparent WPF window using a `Canvas` and `Line`. Position it at `SystemParameters.VirtualScreenLeft/Top` with `SystemParameters.VirtualScreenWidth/Height`, set `Topmost = true`, `ShowInTaskbar = false`, `ShowActivated = false`, `IsHitTestVisible = false`, and use a yellow `Stroke`, `StrokeThickness = 1`, and `StrokeDashArray = new DoubleCollection { 4, 3 }`.

- [ ] **Step 2: Add manager lifecycle methods and two-second expiry**

Add `SetGuideSegment(GuideSegment? segment)` to `SelectionOverlayManager`. Lazily create/show the guide window when a segment exists, update its line endpoints using virtual-screen-relative coordinates, and hide/close it when the segment is null. Start a two-second `DispatcherTimer` whenever a non-null segment is shown; its tick must clear and hide the guide window. Keep `Show()` and `Close()` responsible for the click-selection windows; `Close()` must also clear the guide line and stop the timer.

- [ ] **Step 3: Synchronize controller changes**

Subscribe to `_controller.Changed` in `App.OnStartup` or use the existing main-window update callback to call `_selection.SetGuideSegment(_controller.GuideSegment)`. Update the synchronization after hotkey actions and selection clicks so the line appears as soon as the second point is recorded and disappears on reset/cancel.

- [ ] **Step 4: Keep selection input working**

Ensure the guide window cannot receive mouse input. The existing `SelectionOverlayWindow` instances remain the only windows that handle `MouseLeftButtonDown`.

- [ ] **Step 5: Run a Release build**

Run:

```powershell
& .\.dotnet\dotnet.exe build .\src\PubgMortarRanger\PubgMortarRanger.csproj -c Release --no-restore
```

Expected: build succeeds with zero warnings and zero errors.

### Task 4: Wire the new hotkey and update user-facing hints

**Files:**
- Modify: `src/PubgMortarRanger/App.xaml.cs`
- Modify: `src/PubgMortarRanger/MainWindow.xaml.cs`
- Modify: `src/PubgMortarRanger/MainWindow.xaml`

- [ ] **Step 1: Add the hotkey handler**

Handle `HotkeyAction.Recalibrate` before point-recording actions:

```csharp
case HotkeyAction.Recalibrate:
    _controller.BeginCalibration();
    _selection.SetGuideSegment(null);
    _selection.PointSelected -= OnSelectionPoint;
    _selection.PointSelected += OnSelectionPoint;
    _selection.Show();
    break;
```

The handler must clear the old guide segment and immediately show the multi-monitor selection overlay.

- [ ] **Step 2: Update click-selection synchronization**

After `OnSelectionPoint` records a point, call `_selection.SetGuideSegment(_controller.GuideSegment)`. On calibration completion, measurement completion, cancel, clear, and new selection start, synchronize the nullable segment so stale lines cannot remain.

- [ ] **Step 3: Show the recalibration key in the main window**

Add the formatted recalibration key to the idle hint, for example:

```csharp
_ => $"{HotkeyText(HotkeyAction.BeginClickSelection)} 两次点击标定/测距 | " +
     $"{HotkeyText(HotkeyAction.Recalibrate)} 重新标定 | " +
     $"{HotkeyText(HotkeyAction.ClearMeasurement)} 清除"
```

- [ ] **Step 4: Run the full Release test suite**

Run:

```powershell
& .\.dotnet\dotnet.exe test .\tests\PubgMortarRanger.Tests\PubgMortarRanger.Tests.csproj -c Release --no-restore
```

Expected: every test passes.

### Task 5: Package and verify the updated application

**Files:**
- Modify: `installer/版本说明.txt` if the release notes are maintained in source
- Modify: `docs` usage instructions if the current instructions list the old hotkeys

- [ ] **Step 1: Build the installer payload**

Use the repository's existing Release publish and Inno Setup scripts; do not include `.dotnet`, `.tools`, `bin`, `obj`, or test artifacts.

- [ ] **Step 2: Smoke-test the installer**

Install the generated setup package silently, verify `MortarRangefinder.exe` exists, then uninstall silently. Confirm the executable starts without an immediate exception.

- [ ] **Step 3: Record the new artifact**

Write the updated installer to `E:\Users\Administrator\Desktop\个人项目\Mortar Rangefinder-安装包` and calculate its SHA-256 hash.
