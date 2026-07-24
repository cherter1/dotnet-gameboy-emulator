using GbaEmulator.Core.Input;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Tests.Memory;

public sealed class GbaBusTests
{
    [Fact]
    public void Ewram_IsMirroredAcrossRegion()
    {
        var bus = new GbaBus(new GbaMemory());
        bus.Write32(0x02000000, 0x12345678);

        Assert.Equal(0x12345678U, bus.Read32(0x02040000));
    }

    [Fact]
    public void KeypadRegister_UsesActiveLowBits()
    {
        GbaMemory memory = new();
        var keypad = new KeypadState(memory);
        var bus = new GbaBus(memory);
        keypad.SetPressed(GbaButton.A, true);

        Assert.Equal((ushort)0x03FE, bus.Read16(0x04000130));
    }
}
