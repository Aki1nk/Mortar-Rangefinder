namespace PubgMortarRanger.Input;

public interface IHotkeyRegistrar
{
    bool TryRegister(int id, HotkeyGesture gesture);

    void Unregister(int id);
}
