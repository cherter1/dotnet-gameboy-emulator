using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Input;

public sealed class KeypadState
{
    private readonly GbaMemory _memory;

    public KeypadState(GbaMemory memory)
    {
        _memory = memory;
    }
    
    private ushort _state = 0x03FF;

    public ushort ReadKeyInput() => _state;

    public void SetPressed(GbaButton button, bool pressed)
    {
        var mask = (ushort)(1 << (int)button);
        _memory.Io.REG_KEYINPUT = pressed ? (ushort)(_memory.Io.REG_KEYINPUT & ~mask) : (ushort)(_memory.Io.REG_KEYINPUT | mask);
    }
}
