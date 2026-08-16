namespace PubgMortarRanger.Core;

public readonly record struct ScreenPoint(double X, double Y)
{
    public double DistanceTo(ScreenPoint other)
    {
        var deltaX = other.X - X;
        var deltaY = other.Y - Y;
        var absoluteX = Math.Abs(deltaX);
        var absoluteY = Math.Abs(deltaY);
        var maximum = Math.Max(absoluteX, absoluteY);

        if (maximum == 0d || double.IsInfinity(maximum))
        {
            return maximum;
        }

        var minimum = Math.Min(absoluteX, absoluteY);
        var ratio = minimum / maximum;
        return maximum * Math.Sqrt(1 + (ratio * ratio));
    }
}
