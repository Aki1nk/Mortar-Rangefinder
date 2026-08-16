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
