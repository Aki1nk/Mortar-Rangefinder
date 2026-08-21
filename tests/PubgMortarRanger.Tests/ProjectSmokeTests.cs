using PubgMortarRanger;
using System.Reflection;

namespace PubgMortarRanger.Tests;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void AppAssemblyHasExpectedName()
    {
        Assert.Equal("MortarRangefinder", typeof(App).Assembly.GetName().Name);
    }

    [Fact]
    public void GuideOverlayDuration_IsTwoSeconds()
    {
        var managerType = typeof(App).Assembly.GetType(
            "PubgMortarRanger.SelectionOverlayManager");
        var durationField = managerType?.GetField(
            "GuideDisplayDuration",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(durationField);
        Assert.Equal(TimeSpan.FromSeconds(2), durationField.GetValue(null));
    }
}
