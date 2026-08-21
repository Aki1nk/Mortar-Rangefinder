# Mortar Recalibration and Guide Line Design

## Goal

Add a configurable global hotkey that immediately restarts calibration, and show a thin yellow dashed line between the two selected points during calibration and measurement.

## Behavior

- Add `HotkeyAction.Recalibrate`, with default binding `Ctrl+F8`.
- Pressing the recalibration hotkey clears the current measurement and transient selection state, then enters the two-point calibration flow.
- Existing settings migration must add the new action to older settings files without discarding the user's existing bindings.
- The settings window must display and capture the new action like the existing hotkeys.
- A guide line is shown after two points are selected:
  - calibration: between the two calibration points;
  - measurement: between the mortar and target points.
- The line is a thin yellow dashed stroke spanning the virtual desktop, including points on different monitors.
- The guide line is non-interactive and does not block mouse selection.
- The line automatically disappears two seconds after it is shown.
- Starting a new calibration or measurement, cancelling, clearing, or exiting removes the guide line.

## Architecture

`RangingController` exposes the currently completed two-point guide segment as a nullable value object. `App` synchronizes that segment to `SelectionOverlayManager`. The manager owns a separate transparent, hit-test-disabled guide window covering the Windows virtual desktop; this avoids clipping problems when the two points lie on different monitors while leaving the existing per-monitor selection windows responsible for mouse input.

## Testing

- Unit-test the new default and settings migration behavior.
- Unit-test recalibration resetting a previous calibration/measurement and entering the first calibration state.
- Unit-test guide segment exposure for completed calibration and measurement pairs, and clearing on reset/cancel.
- Run the full Release test suite and build the installer application.
