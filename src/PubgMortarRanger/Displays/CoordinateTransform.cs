using PubgMortarRanger.Core;

namespace PubgMortarRanger.Displays;

public static class CoordinateTransform
{
    public static ScreenPoint PhysicalToDip(
        ScreenPoint physicalPoint,
        ScreenPoint physicalOrigin,
        double scaleX,
        double scaleY) =>
        new(
            (physicalPoint.X - physicalOrigin.X) / scaleX,
            (physicalPoint.Y - physicalOrigin.Y) / scaleY);

    public static ScreenPoint DipToPhysical(
        ScreenPoint dipPoint,
        ScreenPoint physicalOrigin,
        double scaleX,
        double scaleY) =>
        new(
            physicalOrigin.X + (dipPoint.X * scaleX),
            physicalOrigin.Y + (dipPoint.Y * scaleY));
}
