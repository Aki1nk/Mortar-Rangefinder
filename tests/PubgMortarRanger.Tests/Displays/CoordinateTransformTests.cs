using PubgMortarRanger.Core;
using PubgMortarRanger.Displays;

namespace PubgMortarRanger.Tests.Displays;

public sealed class CoordinateTransformTests
{
    [Fact]
    public void PhysicalToDip_ConvertsNegativeVirtualDesktopCoordinates()
    {
        var result = CoordinateTransform.PhysicalToDip(
            new ScreenPoint(-1280, 300),
            new ScreenPoint(-1920, 0),
            1.25,
            1.5);

        Assert.Equal(512, result.X, 10);
        Assert.Equal(200, result.Y, 10);
    }

    [Fact]
    public void DipToPhysical_ConvertsBackUsingPerAxisDpiScale()
    {
        var result = CoordinateTransform.DipToPhysical(
            new ScreenPoint(512, 200),
            new ScreenPoint(-1920, 0),
            1.25,
            1.5);

        Assert.Equal(-1280, result.X, 10);
        Assert.Equal(300, result.Y, 10);
    }
}
