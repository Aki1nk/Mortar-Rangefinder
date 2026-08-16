using PubgMortarRanger;

namespace PubgMortarRanger.Tests;

public sealed class ProjectSmokeTests
{
    [Fact]
    public void AppAssemblyHasExpectedName()
    {
        Assert.Equal("MortarRangefinder", typeof(App).Assembly.GetName().Name);
    }
}
