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
        "display-a",
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
        var mortarPoint = new ScreenPoint(0, 0);
        var targetPoint = new ScreenPoint(targetX, targetY);
        var beforeMeasure = DateTimeOffset.UtcNow;

        var result = _service.Measure(
            mortarPoint,
            targetPoint,
            _calibration,
            121,
            700);

        Assert.Equal(mortarPoint, result.MortarPoint);
        Assert.Equal(targetPoint, result.TargetPoint);
        Assert.Equal(targetX, result.DeltaX, 10);
        Assert.Equal(targetY, result.DeltaY, 10);
        Assert.Equal(expectedMeters, result.DistanceMeters, 10);
        Assert.Equal(expectedBearing, result.BearingDegrees, 10);
        Assert.Equal(TimeSpan.Zero, result.MeasuredAtUtc.Offset);
        Assert.True(result.MeasuredAtUtc >= beforeMeasure);
    }

    [Fact]
    public void Measure_AppliesCalibrationScaleToDiagonalDistance()
    {
        var calibration = CreateCalibration(2.5);

        var result = _service.Measure(
            new ScreenPoint(10, 20),
            new ScreenPoint(13, 24),
            calibration,
            0,
            100);

        Assert.Equal(12.5, result.DistanceMeters, 10);
    }

    [Fact]
    public void Measure_RejectsDistanceThatOverflowsToInfinity()
    {
        var calibration = CreateCalibration(1e200);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(1e200, 0),
                calibration,
                0,
                double.MaxValue));

        Assert.Equal("calibration", exception.ParamName);
    }

    [Fact]
    public void Measure_RejectsDistanceThatUnderflowsToZero()
    {
        var calibration = CreateCalibration(0.5);
        double? returnedDistance = null;

        var exception = Record.Exception(() =>
        {
            var result = _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(double.Epsilon, 0),
                calibration,
                0,
                100);
            returnedDistance = result.DistanceMeters;
        });

        Assert.True(
            exception is not null,
            $"Expected an exception, but measurement returned {returnedDistance:R} meters.");
        var rangeException = Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal("calibration", rangeException.ParamName);
    }

    [Theory]
    [InlineData(1, -1, 45)]
    [InlineData(1, 1, 135)]
    [InlineData(-1, 1, 225)]
    [InlineData(-1, -1, 315)]
    public void Measure_ReturnsNonAxisBearingInEveryQuadrant(
        double targetX,
        double targetY,
        double expectedBearing)
    {
        var result = _service.Measure(
            new ScreenPoint(0, 0),
            new ScreenPoint(targetX, targetY),
            _calibration,
            0,
            100);

        Assert.Equal(expectedBearing, result.BearingDegrees, 10);
    }

    [Theory]
    [InlineData(120, RangeStatus.TooClose)]
    [InlineData(121, RangeStatus.InRange)]
    [InlineData(700, RangeStatus.InRange)]
    [InlineData(701, RangeStatus.TooFar)]
    public void Measure_ClassifiesRangeBoundaries(double meters, RangeStatus expectedStatus)
    {
        var result = _service.Measure(
            new ScreenPoint(0, 0),
            new ScreenPoint(meters, 0),
            _calibration,
            121,
            700);

        Assert.Equal(expectedStatus, result.RangeStatus);
    }

    [Fact]
    public void Measure_RejectsNullCalibration()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                null!,
                121,
                700));

        Assert.Equal("calibration", exception.ParamName);
    }

    [Fact]
    public void Measure_RejectsNegativeMinimumRange()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                _calibration,
                -1,
                700));

        Assert.Equal("minimumRangeMeters", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Measure_RejectsNonFiniteMinimumRange(double minimumRangeMeters)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                _calibration,
                minimumRangeMeters,
                700));

        Assert.Equal("minimumRangeMeters", exception.ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Measure_RejectsNonFiniteMaximumRange(double maximumRangeMeters)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                _calibration,
                121,
                maximumRangeMeters));

        Assert.Equal("maximumRangeMeters", exception.ParamName);
    }

    [Fact]
    public void Measure_RejectsMaximumRangeBelowMinimum()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                _calibration,
                121,
                120));

        Assert.Equal("maximumRangeMeters", exception.ParamName);
    }

    [Theory]
    [InlineData(true, true, double.NaN)]
    [InlineData(true, true, double.PositiveInfinity)]
    [InlineData(true, true, double.NegativeInfinity)]
    [InlineData(true, false, double.NaN)]
    [InlineData(true, false, double.PositiveInfinity)]
    [InlineData(true, false, double.NegativeInfinity)]
    [InlineData(false, true, double.NaN)]
    [InlineData(false, true, double.PositiveInfinity)]
    [InlineData(false, true, double.NegativeInfinity)]
    [InlineData(false, false, double.NaN)]
    [InlineData(false, false, double.PositiveInfinity)]
    [InlineData(false, false, double.NegativeInfinity)]
    public void Measure_RejectsNonFinitePointCoordinates(
        bool invalidMortarPoint,
        bool invalidX,
        double invalidCoordinate)
    {
        var invalidPoint = invalidX
            ? new ScreenPoint(invalidCoordinate, 0)
            : new ScreenPoint(0, invalidCoordinate);
        var mortarPoint = invalidMortarPoint ? invalidPoint : new ScreenPoint(0, 0);
        var targetPoint = invalidMortarPoint ? new ScreenPoint(100, 0) : invalidPoint;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(mortarPoint, targetPoint, _calibration, 121, 700));

        Assert.Equal(
            invalidMortarPoint ? "mortarPoint" : "targetPoint",
            exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Measure_RejectsInvalidCalibrationScale(double metersPerPixel)
    {
        var calibration = CreateCalibration(metersPerPixel);

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Measure(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                calibration,
                121,
                700));

        Assert.Equal("calibration", exception.ParamName);
    }

    [Fact]
    public void Measure_RejectsCoincidentPoints()
    {
        var point = new ScreenPoint(25, 50);

        var exception = Assert.Throws<ArgumentException>(() =>
            _service.Measure(point, point, _calibration, 121, 700));

        Assert.Equal("targetPoint", exception.ParamName);
    }

    private static CalibrationProfile CreateCalibration(double metersPerPixel)
    {
        return new CalibrationProfile(
            new ScreenPoint(0, 0),
            new ScreenPoint(100, 0),
            100,
            metersPerPixel,
            "display-a",
            DateTimeOffset.UnixEpoch);
    }
}
