using PubgMortarRanger.Displays;

namespace PubgMortarRanger.Tests.Displays;

public sealed class DisplayFingerprintTests
{
    [Fact]
    public void StableValue_IncludesDeviceBoundsAndDpi()
    {
        var fingerprint = new DisplayFingerprint(
            @"\\.\DISPLAY2",
            -1920,
            0,
            1920,
            1080,
            120,
            144);

        Assert.Equal(
            @"\\.\DISPLAY2|-1920,0,1920,1080|120x144",
            fingerprint.StableValue);
    }

    [Fact]
    public void StableValue_ChangesWhenDpiChanges()
    {
        var original = new DisplayFingerprint(
            @"\\.\DISPLAY1",
            0,
            0,
            1920,
            1080,
            96,
            96);

        var changed = original with { DpiX = 120, DpiY = 120 };

        Assert.NotEqual(original.StableValue, changed.StableValue);
    }
}
