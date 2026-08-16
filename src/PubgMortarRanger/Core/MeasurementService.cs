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

        ValidateFinitePoint(mortarPoint, nameof(mortarPoint));
        ValidateFinitePoint(targetPoint, nameof(targetPoint));

        if (!double.IsFinite(calibration.MetersPerPixel) ||
            calibration.MetersPerPixel <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(calibration),
                calibration.MetersPerPixel,
                "标定比例必须是大于零的有限数值。");
        }

        if (!double.IsFinite(minimumRangeMeters) || minimumRangeMeters < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumRangeMeters),
                minimumRangeMeters,
                "最小射程必须是非负有限数值。");
        }

        if (!double.IsFinite(maximumRangeMeters))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRangeMeters),
                maximumRangeMeters,
                "最大射程必须是有限数值。");
        }

        if (maximumRangeMeters < minimumRangeMeters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRangeMeters),
                maximumRangeMeters,
                "最大射程不能小于最小射程。");
        }

        if (mortarPoint == targetPoint)
        {
            throw new ArgumentException(
                "炮位点与目标点不能重合。",
                nameof(targetPoint));
        }

        var deltaX = targetPoint.X - mortarPoint.X;
        var deltaY = targetPoint.Y - mortarPoint.Y;
        var pixelDistance = mortarPoint.DistanceTo(targetPoint);
        var distanceMeters = pixelDistance * calibration.MetersPerPixel;
        if (!double.IsFinite(distanceMeters) || distanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(calibration),
                calibration.MetersPerPixel,
                "标定比例与像素距离产生了不可表示的测量距离。");
        }

        var bearingDegrees = NormalizeDegrees(
            Math.Atan2(deltaX, -deltaY) * (180 / Math.PI));
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
            bearingDegrees,
            rangeStatus,
            DateTimeOffset.UtcNow);
    }

    private static double NormalizeDegrees(double degrees)
    {
        return degrees < 0 ? degrees + 360 : degrees;
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
