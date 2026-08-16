using PubgMortarRanger.Input;

namespace PubgMortarRanger.Tests.Input;

public sealed class GlobalHotkeyServiceTests
{
    [Fact]
    public void Apply_RestoresPreviousRegistrations_WhenNewRegistrationFails()
    {
        var failingGesture = new HotkeyGesture(HotkeyModifiers.Alt, 0x7C);
        var registrar = new FakeHotkeyRegistrar(failingGesture);
        using var service = new GlobalHotkeyService(registrar);
        var original = HotkeyGesture.CreateDefaults().ToDictionary();

        Assert.True(service.Apply(original).IsValid);

        var replacement = original.ToDictionary();
        replacement[HotkeyAction.RecordMortar] =
            new HotkeyGesture(HotkeyModifiers.Alt, 0x7B);
        replacement[HotkeyAction.RecordTarget] = failingGesture;

        var result = service.Apply(replacement);

        Assert.False(result.IsValid);
        Assert.Equal(original, service.ActiveBindings);
        Assert.Equal(
            original.Where(pair => pair.Value.IsGlobal)
                .ToDictionary(pair => (int)pair.Key + 1, pair => pair.Value),
            registrar.Registrations);
    }

    [Fact]
    public void Apply_LeavesPreviousRegistrations_WhenValidationFails()
    {
        var registrar = new FakeHotkeyRegistrar();
        using var service = new GlobalHotkeyService(registrar);
        var original = HotkeyGesture.CreateDefaults().ToDictionary();
        Assert.True(service.Apply(original).IsValid);
        var invalid = original.ToDictionary();
        invalid[HotkeyAction.RecordTarget] = invalid[HotkeyAction.RecordMortar];

        var result = service.Apply(invalid);

        Assert.False(result.IsValid);
        Assert.Equal(original, service.ActiveBindings);
        Assert.Equal(
            original.Where(pair => pair.Value.IsGlobal)
                .ToDictionary(pair => (int)pair.Key + 1, pair => pair.Value),
            registrar.Registrations);
    }

    [Fact]
    public void SuspendAndResume_ReleasesRegistrationsWithoutLosingBindings()
    {
        var registrar = new FakeHotkeyRegistrar();
        using var service = new GlobalHotkeyService(registrar);
        var bindings = HotkeyGesture.CreateDefaults().ToDictionary();
        Assert.True(service.Apply(bindings).IsValid);

        service.Suspend();

        Assert.Equal(bindings, service.ActiveBindings);
        Assert.Empty(registrar.Registrations);

        var result = service.Resume();

        Assert.True(result.IsValid);
        Assert.Equal(
            bindings.Where(pair => pair.Value.IsGlobal)
                .ToDictionary(pair => (int)pair.Key + 1, pair => pair.Value),
            registrar.Registrations);
    }

    private sealed class FakeHotkeyRegistrar(HotkeyGesture? failingGesture = null)
        : IHotkeyRegistrar
    {
        public Dictionary<int, HotkeyGesture> Registrations { get; } = [];

        public bool TryRegister(int id, HotkeyGesture gesture)
        {
            if (gesture == failingGesture)
            {
                return false;
            }

            Registrations.Add(id, gesture);
            return true;
        }

        public void Unregister(int id)
        {
            Registrations.Remove(id);
        }
    }
}
