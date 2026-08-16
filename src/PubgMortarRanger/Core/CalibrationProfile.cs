namespace PubgMortarRanger.Core;

public sealed record CalibrationProfile(
    ScreenPoint FirstPoint,
    ScreenPoint SecondPoint,
    double KnownMeters,
    double MetersPerPixel,
    string DisplayFingerprint,
    DateTimeOffset CreatedAtUtc);
