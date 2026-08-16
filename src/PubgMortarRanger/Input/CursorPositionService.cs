using PubgMortarRanger.Core;
using PubgMortarRanger.Interop;

namespace PubgMortarRanger.Input;

public sealed class CursorPositionService
{
    public ScreenPoint GetPhysicalPosition()
    {
        if (!NativeMethods.GetCursorPos(out var cursorPosition))
        {
            throw new InvalidOperationException("无法获取物理鼠标坐标。");
        }

        return new ScreenPoint(cursorPosition.X, cursorPosition.Y);
    }
}
