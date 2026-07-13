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
        var memory = new GbaMemory();
        var interrupts = new InterruptController(memory);
        var dma = new DmaController(interrupts, memory);
        var bus = new GbaBus(interrupts, new TimerController(interrupts, memory), dma, new Ppu(interrupts, dma, memory), new KeypadState(memory), memory);
        return (new Arm7Tdmi(bus, interrupts), bus);
    }
}