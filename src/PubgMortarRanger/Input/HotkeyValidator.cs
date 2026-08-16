namespace PubgMortarRanger.Input;

public static class HotkeyValidator
{
    public static HotkeyValidationResult Validate(
        IReadOnlyDictionary<HotkeyAction, HotkeyGesture> bindings)
    {
        var missingActions = Enum.GetValues<HotkeyAction>()
            .Where(action => !bindings.ContainsKey(action))
            .ToArray();

        if (missingActions.Length > 0)
        {
            return HotkeyValidationResult.Failure(
                $"Missing hotkey bindings: {string.Join(", ", missingActions)}.");
        }

        var hasDuplicateGlobalGesture = bindings
            .Where(pair => pair.Value.IsGlobal)
            .GroupBy(pair => (pair.Value.Modifiers, pair.Value.VirtualKey))
            .Any(group => group.Skip(1).Any());

        return hasDuplicateGlobalGesture
            ? HotkeyValidationResult.Failure("Duplicate global hotkey gestures.")
            : HotkeyValidationResult.Success;
    }
}
