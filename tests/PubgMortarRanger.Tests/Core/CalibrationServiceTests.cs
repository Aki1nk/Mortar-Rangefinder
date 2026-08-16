using PubgMortarRanger.Core;

namespace PubgMortarRanger.Tests.Core;

public sealed class CalibrationServiceTests
{
    private readonly CalibrationService _service = new();

    [Fact]
    public void ScreenPoint_DistanceTo_AvoidsOverflowForLargeFiniteCoordinates()
    {
        var distance = new ScreenPoint(0, 0)
            .DistanceTo(new ScreenPoint(1e200, 0));

        Assert.True(double.IsFinite(distance));
        Assert.InRange(Math.Abs((distance - 1e200) / 1e200), 0, 1e-12);
    }

    [Fact]
    public void Create_DoesNotTreatDoubleEpsilonSeparationAsCoincident()
    {
        var result = _service.Create(
            new ScreenPoint(0, 0),
            new ScreenPoint(double.Epsilon, 0),
            double.Epsilon,
            "display-a");

        Assert.Equal(1, result.MetersPerPixel);
    }

    [Fact]
    public void Create_RejectsCalibrationScaleThatUnderflowsToZero()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(
                new ScreenPoint(0, 0),
                new ScreenPoint(1e200, 0),
                double.Epsilon,
                "display-a"));

        Assert.Equal("knownMeters", exception.ParamName);
    }

    [Fact]
    public void Create_RejectsCalibrationScaleThatOverflowsToInfinity()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(
                new ScreenPoint(0, 0),
                new ScreenPoint(double.Epsilon, 0),
                1,
                "display-a"));

        Assert.Equal("knownMeters", exception.ParamName);
    }

    [Fact]
    public void Create_UsesEuclideanPixelDistance()
    {
        var beforeCreate = DateTimeOffset.UtcNow;

        var result = _service.Create(
            new ScreenPoint(10, 20),
            new ScreenPoint(310, 420),
            500,
            "display-a");

        Assert.Equal(1, result.MetersPerPixel, 10);
        Assert.Equal(500, result.KnownMeters);
        Assert.Equal("display-a", result.DisplayFingerprint);
        Assert.Equal(TimeSpan.Zero, result.CreatedAtUtc.Offset);
        Assert.True(result.CreatedAtUtc >= beforeCreate);
    }

    [Fact]
    public void IsValidFor_RequiresAnOrdinalFingerprintMatch()
    {
        var profile = _service.Create(
            new ScreenPoint(0, 0),
            new ScreenPoint(100, 0),
            100,
            "display-a");

        Assert.True(_service.IsValidFor(profile, "display-a"));
        Assert.False(_service.IsValidFor(profile, "DISPLAY-A"));
    }

    [Fact]
    public void Create_RejectsCoincidentPoints()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.Create(
                new ScreenPoint(5, 5),
                new ScreenPoint(5, 5),
                100,
                "display-a"));

        Assert.Equal("secondPoint", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Create_RejectsInvalidKnownMeters(double knownMeters)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                knownMeters,
                "display-a"));

        Assert.Equal("knownMeters", exception.ParamName);
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
    public void Create_RejectsNonFinitePointCoordinates(
        bool invalidFirstPoint,
        bool invalidX,
        double invalidCoordinate)
    {
        var invalidPoint = invalidX
            ? new ScreenPoint(invalidCoordinate, 0)
            : new ScreenPoint(0, invalidCoordinate);
        var firstPoint = invalidFirstPoint ? invalidPoint : new ScreenPoint(0, 0);
        var secondPoint = invalidFirstPoint ? new ScreenPoint(100, 0) : invalidPoint;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(firstPoint, secondPoint, 100, "display-a"));

        Assert.Equal(
            invalidFirstPoint ? "firstPoint" : "secondPoint",
            exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_RejectsBlankDisplayFingerprint(string displayFingerprint)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            _service.Create(
                new ScreenPoint(0, 0),
                new ScreenPoint(100, 0),
                100,
                displayFingerprint));

        Assert.Equal("displayFingerprint", exception.ParamName);
    }
}
