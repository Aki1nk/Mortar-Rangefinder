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

        if (TryRegisterAll(
                bindings,
                out var registeredActions,
                out var failedAction))
        {
            _activeBindings = bindings.ToDictionary();
            _isSuspended = false;
            return HotkeyValidationResult.Success;
        }

        UnregisterAll(registeredActions);
        TryRegisterAll(previousBindings, out _, out _);
        _activeBindings = previousBindings;
        _isSuspended = false;

        return CreateRegistrationFailure(
            failedAction,
            bindings,
            "之前的热键设置已恢复。");
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

        if (TryRegisterAll(
                _activeBindings,
                out var registeredActions,
                out var failedAction))
        {
            _isSuspended = false;
            return HotkeyValidationResult.Success;
        }

        UnregisterAll(registeredActions);
        return CreateRegistrationFailure(
            failedAction,
            _activeBindings,
            "热键目前保持暂停状态。");
    }

    public void Dispose()
    {
        UnregisterAll(_activeBindings.Keys);
        _activeBindings = [];
        _isSuspended = false;
    }

    private bool TryRegisterAll(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings,
        out List<HotkeyAction> registeredActions,
        out HotkeyAction? failedAction)
    {
        registeredActions = [];
        failedAction = null;

        foreach (var (action, gesture) in bindings.Where(pair => pair.Value.IsGlobal))
        {
            if (!_registrar.TryRegister(ToRegistrationId(action), gesture))
            {
                failedAction = action;
                return false;
            }

            registeredActions.Add(action);
        }

        return true;
    }

    private static HotkeyValidationResult CreateRegistrationFailure(
        HotkeyAction? failedAction,
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings,
        string suffix)
    {
        if (failedAction is not { } action ||
            !bindings.TryGetValue(action, out var gesture))
        {
            return HotkeyValidationResult.Failure(
                $"Windows 拒绝注册热键。{suffix}");
        }

        return HotkeyValidationResult.Failure(
            $"Windows 拒绝注册“{HotkeyDisplayFormatter.FormatAction(action)}”热键" +
            $"（{HotkeyDisplayFormatter.Format(gesture)}），该按键可能已被其他程序占用。{suffix}");
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
