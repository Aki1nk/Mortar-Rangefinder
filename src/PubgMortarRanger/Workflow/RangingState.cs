namespace PubgMortarRanger.Workflow;

public enum RangingState
{
    Uncalibrated,
    Ready,
    AwaitingCalibrationFirstPoint,
    AwaitingCalibrationSecondPoint,
    AwaitingCalibrationDistance,
    AwaitingMortarPoint,
    AwaitingTargetPoint,
    ShowingResult
}
