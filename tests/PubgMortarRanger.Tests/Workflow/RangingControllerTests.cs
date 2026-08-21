using PubgMortarRanger.Core;
using PubgMortarRanger.Workflow;

namespace PubgMortarRanger.Tests.Workflow;

public sealed class RangingControllerTests
{
    [Fact]
    public void CalibrationAndClickFlow_ProducesMeasurement()
    {
        var controller = new RangingController();
        controller.BeginCalibration();
        controller.RecordPoint(new ScreenPoint(0, 0));
        controller.RecordPoint(new ScreenPoint(100, 0));
        controller.CompleteCalibration(100, "display-a");

        controller.BeginClickMeasurement();
        controller.RecordPoint(new ScreenPoint(0, 0));
        var result = controller.RecordPoint(new ScreenPoint(100, 0));

        Assert.NotNull(result);
        Assert.Equal(90, result.BearingDegrees, 10);
        Assert.Equal(RangingState.ShowingResult, controller.State);
    }

    [Fact]
    public void RecordTarget_RejectsMissingMortarPoint()
    {
        var controller = CreateCalibratedController();

        var exception = Assert.Throws<InvalidOperationException>(
            () => controller.RecordTarget(new ScreenPoint(100, 0)));

        Assert.Contains("先记录", exception.Message);
    }

    [Fact]
    public void Cancel_RestoresReadyAndClearRemovesResult()
    {
        var controller = CreateCalibratedController();
        controller.BeginClickMeasurement();
        controller.Cancel();
        Assert.Equal(RangingState.Ready, controller.State);

        controller.RecordMortar(new ScreenPoint(0, 0));
        controller.RecordTarget(new ScreenPoint(100, 0));
        controller.ClearMeasurement();

        Assert.Null(controller.LastMeasurement);
        Assert.Equal(RangingState.Ready, controller.State);
    }

    [Fact]
    public void BeginCalibration_ClearsExistingMeasurementAndGuideLine()
    {
        var controller = CreateCalibratedController();
        controller.RecordMortar(new ScreenPoint(0, 0));
        controller.RecordTarget(new ScreenPoint(100, 0));
        Assert.NotNull(controller.LastMeasurement);

        controller.BeginCalibration();

        Assert.Null(controller.LastMeasurement);
        Assert.Null(controller.GuideSegment);
        Assert.Equal(RangingState.AwaitingCalibrationFirstPoint, controller.State);
    }

    [Fact]
    public void CompletedPointPairs_ExposeGuideSegment()
    {
        var controller = CreateCalibratedController();
        controller.BeginClickMeasurement();
        controller.RecordPoint(new ScreenPoint(10, 20));
        controller.RecordPoint(new ScreenPoint(110, 120));

        Assert.Equal(
            new GuideSegment(new ScreenPoint(10, 20), new ScreenPoint(110, 120)),
            controller.GuideSegment);
    }

    private static RangingController CreateCalibratedController()
    {
        var controller = new RangingController();
        controller.SetCalibration(new CalibrationProfile(
            new ScreenPoint(0, 0),
            new ScreenPoint(100, 0),
            100,
            1,
            "display-a",
            DateTimeOffset.UtcNow));
        return controller;
    }
}
