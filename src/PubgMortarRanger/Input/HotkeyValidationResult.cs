namespace PubgMortarRanger.Input;

public sealed record HotkeyValidationResult(bool IsValid, string? ErrorMessage)
{
    public static HotkeyValidationResult Success { get; } = new(true, null);

    public static HotkeyValidationResult Failure(string errorMessage) =>
        new(false, errorMessage);
}
