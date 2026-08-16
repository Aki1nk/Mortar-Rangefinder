namespace PubgMortarRanger.Core;

public sealed class CalibrationService
{
    public bool IsValidFor(
        CalibrationProfile profile,
        string displayFingerprint) =>
        string.Equals(
            profile.DisplayFingerprint,
            displayFingerprint,
            StringComparison.Ordinal);

    public CalibrationProfile Create(
        ScreenPoint firstPoint,
        ScreenPoint secondPoint,
        double knownMeters,
        string displayFingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayFingerprint);

        if (!double.IsFinite(knownMeters) || knownMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(knownMeters),
                knownMeters,
                "已知距离必须是大于零的有限数值。");
        }

        ValidateFinitePoint(firstPoint, nameof(firstPoint));
        ValidateFinitePoint(secondPoint, nameof(secondPoint));

        var pixelDistance = firstPoint.DistanceTo(secondPoint);
        if (pixelDistance == 0d)
        {
            throw new ArgumentException("标定点不能重合。", nameof(secondPoint));
        }

        var metersPerPixel = knownMeters / pixelDistance;
        if (!double.IsFinite(metersPerPixel) || metersPerPixel <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(knownMeters),
                knownMeters,
                "已知距离与标定点间距无法形成有效标定比例。");
        }

        return new CalibrationProfile(
            firstPoint,
            secondPoint,
            knownMeters,
            metersPerPixel,
            displayFingerprint,
            DateTimeOffset.UtcNow);
    }

    private static void ValidateFinitePoint(ScreenPoint point, string paramName)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                point,
                "屏幕坐标必须是有限数值。");
        }
    }
}
