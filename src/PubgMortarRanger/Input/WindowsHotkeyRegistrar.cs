using PubgMortarRanger.Interop;

namespace PubgMortarRanger.Input;

public sealed class WindowsHotkeyRegistrar(nint windowHandle) : IHotkeyRegistrar
{
    public bool TryRegister(int id, HotkeyGesture gesture) =>
        NativeMethods.RegisterHotKey(
            windowHandle,
            id,
            (uint)gesture.Modifiers,
            (uint)gesture.VirtualKey);

    public void Unregister(int id) => NativeMethods.UnregisterHotKey(windowHandle, id);
}
