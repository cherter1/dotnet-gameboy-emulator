namespace GbaEmulator.Core.Input;

public sealed class KeypadState
{
    private ushort _state = 0x03FF;

    public ushort ReadKeyInput() => _state;

    public void SetPressed(GbaButton button, bool pressed)
    {
        var mask = (ushort)(1 << (int)button);
        _state = pressed ? (ushort)(_state & ~mask) : (ushort)(_state | mask);
    }
}
