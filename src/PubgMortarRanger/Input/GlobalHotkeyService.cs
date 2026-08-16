using System.Collections.ObjectModel;

namespace PubgMortarRanger.Input;

public sealed class GlobalHotkeyService(IHotkeyRegistrar registrar) : IDisposable
{
    private readonly IHotkeyRegistrar _registrar = registrar;
    private Dictionary<HotkeyAction, HotkeyGesture> _activeBindings = [];
    private bool _isSuspended;

    public IReadOnlyDictionary<HotkeyAction, HotkeyGesture> ActiveBindings =>
        new ReadOnlyDictionary<HotkeyAction, HotkeyGesture>(_activeBindings);

    public HotkeyValidationResult Apply(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        var validation = HotkeyValidator.Validate(bindings);
        if (!validation.IsValid)
        {
            return validation;
        }

        var previousBindings = _activeBindings.ToDictionary();
        UnregisterAll(previousBindings.Keys);

        if (TryRegisterAll(bindings, out var registeredActions))
        {
            _activeBindings = bindings.ToDictionary();
            _isSuspended = false;
            return HotkeyValidationResult.Success;
        }

        UnregisterAll(registeredActions);
        TryRegisterAll(previousBindings, out _);
        _activeBindings = previousBindings;
        _isSuspended = false;

        return HotkeyValidationResult.Failure(
            "Windows rejected a hotkey registration; previous bindings were restored.");
    }

    public void Suspend()
    {
        if (_isSuspended)
        {
            return;
        }

        UnregisterAll(_activeBindings.Keys);
        _isSuspended = true;
    }

    public HotkeyValidationResult Resume()
    {
        if (!_isSuspended)
        {
            return HotkeyValidationResult.Success;
        }

        if (TryRegisterAll(_activeBindings, out var registeredActions))
        {
            _isSuspended = false;
            return HotkeyValidationResult.Success;
        }

        UnregisterAll(registeredActions);
        return HotkeyValidationResult.Failure(
            "Windows rejected a hotkey registration; hotkeys remain suspended.");
    }

    public void Dispose()
    {
        UnregisterAll(_activeBindings.Keys);
        _activeBindings = [];
        _isSuspended = false;
    }

    private bool TryRegisterAll(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings,
        out List<HotkeyAction> registeredActions)
    {
        registeredActions = [];

        foreach (var (action, gesture) in bindings.Where(pair => pair.Value.IsGlobal))
        {
            if (!_registrar.TryRegister(ToRegistrationId(action), gesture))
            {
                return false;
            }

            registeredActions.Add(action);
        }

        return true;
    }

    private void UnregisterAll(IEnumerable<HotkeyAction> actions)
    {
        foreach (var action in actions)
        {
            _registrar.Unregister(ToRegistrationId(action));
        }
    }

    private static int ToRegistrationId(HotkeyAction action) => (int)action + 1;
}
