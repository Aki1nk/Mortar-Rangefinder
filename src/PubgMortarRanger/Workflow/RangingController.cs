using PubgMortarRanger.Core;

namespace PubgMortarRanger.Workflow;

public sealed class RangingController
{
    private readonly CalibrationService _calibrationService = new();
    private readonly MeasurementService _measurementService = new();
    private ScreenPoint? _pendingCalibrationFirstPoint;
    private ScreenPoint? _pendingCalibrationSecondPoint;
    private double _minimumRangeMeters = 121;
    private double _maximumRangeMeters = 700;

    public RangingState State { get; private set; } = RangingState.Uncalibrated;
    public CalibrationProfile? Calibration { get; private set; }
    public ScreenPoint? PendingMortarPoint { get; private set; }
    public MeasurementResult? LastMeasurement { get; private set; }
    public event EventHandler? Changed;

    public void SetCalibration(CalibrationProfile? calibration)
    {
        Calibration = calibration;
        ResetTransientState();
        State = calibration is null ? RangingState.Uncalibrated : RangingState.Ready;
        RaiseChanged();
    }

    public void BeginCalibration()
    {
        ResetTransientState();
        State = RangingState.AwaitingCalibrationFirstPoint;
        RaiseChanged();
    }

    public void CompleteCalibration(double knownMeters, string displayFingerprint)
    {
        if (State != RangingState.AwaitingCalibrationDistance ||
            _pendingCalibrationFirstPoint is not { } firstPoint ||
            _pendingCalibrationSecondPoint is not { } secondPoint)
        {
            throw new InvalidOperationException("当前不在等待标定距离的状态。");
        }

        Calibration = _calibrationService.Create(firstPoint, secondPoint, knownMeters, displayFingerprint);
        ResetTransientState();
        State = RangingState.Ready;
        RaiseChanged();
    }

    public void BeginClickMeasurement()
    {
        EnsureCalibrated();
        PendingMortarPoint = null;
        State = RangingState.AwaitingMortarPoint;
        RaiseChanged();
    }

    public void RecordMortar(ScreenPoint point)
    {
        EnsureCalibrated();
        PendingMortarPoint = point;
        State = RangingState.AwaitingTargetPoint;
        RaiseChanged();
    }

    public MeasurementResult RecordTarget(ScreenPoint point)
    {
        EnsureCalibrated();
        if (PendingMortarPoint is not { } mortarPoint)
        {
            throw new InvalidOperationException("请先记录迫击炮位置。");
        }

        var result = Measure(mortarPoint, point);
        PendingMortarPoint = null;
        LastMeasurement = result;
        State = RangingState.ShowingResult;
        RaiseChanged();
        return result;
    }

    public MeasurementResult? RecordPoint(ScreenPoint point)
    {
        switch (State)
        {
            case RangingState.AwaitingCalibrationFirstPoint:
                _pendingCalibrationFirstPoint = point;
                State = RangingState.AwaitingCalibrationSecondPoint;
                RaiseChanged();
                return null;
            case RangingState.AwaitingCalibrationSecondPoint:
                _pendingCalibrationSecondPoint = point;
                State = RangingState.AwaitingCalibrationDistance;
                RaiseChanged();
                return null;
            case RangingState.AwaitingMortarPoint:
                RecordMortar(point);
                return null;
            case RangingState.AwaitingTargetPoint:
                return RecordTarget(point);
            default:
                throw new InvalidOperationException("当前状态不能记录选点。");
        }
    }

    public void UpdateRangeLimits(double minimumRangeMeters, double maximumRangeMeters)
    {
        if (!double.IsFinite(minimumRangeMeters) || minimumRangeMeters < 0 ||
            !double.IsFinite(maximumRangeMeters) || maximumRangeMeters < minimumRangeMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumRangeMeters), "射程范围无效。");
        }

        _minimumRangeMeters = minimumRangeMeters;
        _maximumRangeMeters = maximumRangeMeters;
        if (LastMeasurement is { } measurement && Calibration is { } calibration)
        {
            LastMeasurement = Measure(measurement.MortarPoint, measurement.TargetPoint);
        }

        RaiseChanged();
    }

    public void ClearMeasurement()
    {
        LastMeasurement = null;
        PendingMortarPoint = null;
        State = Calibration is null ? RangingState.Uncalibrated : RangingState.Ready;
        RaiseChanged();
    }

    public void Cancel()
    {
        ResetTransientState();
        State = Calibration is null ? RangingState.Uncalibrated : RangingState.Ready;
        RaiseChanged();
    }

    private MeasurementResult Measure(ScreenPoint mortarPoint, ScreenPoint targetPoint)
    {
        return _measurementService.Measure(
            mortarPoint,
            targetPoint,
            Calibration ?? throw new InvalidOperationException("请先完成地图标定。"),
            _minimumRangeMeters,
            _maximumRangeMeters);
    }

    private void EnsureCalibrated()
    {
        if (Calibration is null)
        {
            throw new InvalidOperationException("请先完成地图标定。");
        }
    }

    private void ResetTransientState()
    {
        PendingMortarPoint = null;
        _pendingCalibrationFirstPoint = null;
        _pendingCalibrationSecondPoint = null;
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
