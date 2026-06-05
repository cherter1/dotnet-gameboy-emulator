using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Input;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Timers;
using GbaEmulator.Core.Video;

namespace GbaEmulator.Core.Tests.TestUtils;

public class CpuUtilities
{
    public static (Arm7Tdmi Cpu, GbaBus Bus) CreateCpu()
    {
        var interrupts = new InterruptController();
        var dma = new DmaController(interrupts);
        var bus = new GbaBus(interrupts, new TimerController(interrupts), dma, new Ppu(interrupts, dma), new KeypadState());
        return (new Arm7Tdmi(bus, interrupts), bus);
    }
}