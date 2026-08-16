namespace PubgMortarRanger.Displays;

public sealed record DisplayFingerprint(
    string DeviceName,
    int Left,
    int Top,
    int Width,
    int Height,
    uint DpiX,
    uint DpiY)
{
    public string StableValue =>
        $"{DeviceName}|{Left},{Top},{Width},{Height}|{DpiX}x{DpiY}";
}
