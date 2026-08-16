using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Input;

public sealed class HotkeyValidatorTests
{
    [Fact]
    public void Validate_RejectsBindingsMissingAnAction()
    {
        var bindings = HotkeyGesture.CreateDefaults().ToDictionary();
        bindings.Remove(HotkeyAction.RecordTarget);

        var result = HotkeyValidator.Validate(bindings);

        Assert.False(result.IsValid);
        Assert.Contains(nameof(HotkeyAction.RecordTarget), result.ErrorMessage);
    }

    [Fact]
    public void Validate_RejectsDuplicateGlobalGestures()
    {
        var bindings = HotkeyGesture.CreateDefaults().ToDictionary();
        bindings[HotkeyAction.RecordTarget] = bindings[HotkeyAction.RecordMortar];

        var result = HotkeyValidator.Validate(bindings);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Validate_IgnoresDuplicateLocalGesture()
    {
        var bindings = HotkeyGesture.CreateDefaults().ToDictionary();
        bindings[HotkeyAction.CancelCurrent] =
            bindings[HotkeyAction.RecordMortar] with { IsGlobal = false };

        var result = HotkeyValidator.Validate(bindings);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }
}
