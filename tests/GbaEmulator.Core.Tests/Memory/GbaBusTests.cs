using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Input;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Timers;
using GbaEmulator.Core.Video;

namespace GbaEmulator.Core.Tests.Memory;

public sealed class GbaBusTests
{
    [Fact]
    public void Ewram_IsMirroredAcrossRegion()
    {
        var bus = CreateBus();
        bus.Write32(0x02000000, 0x12345678);

        Assert.Equal(0x12345678U, bus.Read32(0x02040000));
    }

    [Fact]
    public void KeypadRegister_UsesActiveLowBits()
    {
        var keypad = new KeypadState();
        var bus = CreateBus(keypad);
        keypad.SetPressed(GbaButton.A, true);

        Assert.Equal((ushort)0x03FE, bus.Read16(0x04000130));
    }

    private static GbaBus CreateBus(KeypadState? keypad = null)
    {
        var interrupts = new InterruptController();
        var dma = new DmaController(interrupts);
        keypad ??= new KeypadState();
        return new GbaBus(interrupts, new TimerController(interrupts), dma, new Ppu(interrupts, dma), keypad);
    }
}
