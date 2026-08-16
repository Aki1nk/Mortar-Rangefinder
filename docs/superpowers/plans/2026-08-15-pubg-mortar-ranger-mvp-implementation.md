# PUBG 迫击炮测距工具 MVP Implementation Plan

> **For agentic workers:** Use streamlined subagent implementation. Each task requires targeted tests and a build; exhaustive two-stage review is intentionally deferred for this personal-use MVP.

**Goal:** Deliver a usable multi-monitor WPF mortar ranging overlay with editable hotkeys.

**Architecture:** Reuse existing Core and Configuration modules. Add Windows interop for cursor, DPI, and hotkeys; put state transitions in `Workflow`; keep WPF windows thin and route all points through physical screen coordinates.

**Tech Stack:** C# 14, .NET 10, WPF, Win32 P/Invoke, xUnit.

---

### Task 1: Finish display and global-input foundations

Create display fingerprints, physical/DIP transforms, cursor service, hotkey validation/registration, and targeted unit tests. Register only configured keys; use `WM_HOTKEY` dispatch from the application HWND.

### Task 2: Add ranging workflow

Create a tested controller for calibration and measurement states. It must accept points from either hotkeys or click selection and expose the latest result to presentation code.

### Task 3: Build the overlay and selection windows

Create the compact topmost result bar, click-through style helper, per-monitor transparent selection windows, calibration-distance dialog, and physical-coordinate conversion.

### Task 4: Add editable settings UI and wire the app

Create a settings window for all hotkeys and basic range/appearance values. Compose settings, controller, windows, hotkeys, and persistence in `App.xaml.cs`. Do not add tray, single-instance, or history.

### Task 5: Manual MVP validation

Run targeted tests and a Release build. Manually verify two-monitor selection, calibration, hotkeys, settings persistence, cancellation, and click-through restoration.
