using System.Diagnostics;
using GbaEmulator.Core.Bios;
using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Dma;
using GbaEmulator.Core.Input;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Timers;
using GbaEmulator.Core.Video;
using GbaCartridge = GbaEmulator.Core.Cartridge.Cartridge;

namespace GbaEmulator.Core;

public sealed class GbaMachine
{
    private readonly bool _skipBios;
    public Arm7Tdmi Cpu { get; }
    public GbaBus Bus { get; }
    public Ppu Ppu { get; }
    private TimerController Timers { get; }
    public DmaController Dma { get; }
    public InterruptController Interrupts { get; }
    public KeypadState Keypad { get; }
    public GbaCartridge? Cartridge { get; }
    public FrameBuffer FrameBuffer => Ppu.FrameBuffer;

    private GbaMachine(
        Arm7Tdmi cpu,
        GbaBus bus,
        Ppu ppu,
        TimerController timers,
        DmaController dma,
        InterruptController interrupts,
        KeypadState keypad,
        GbaCartridge? cartridge,
        bool skipBios)
    {
        Cpu = cpu;
        Bus = bus;
        Ppu = ppu;
        Timers = timers;
        Dma = dma;
        Interrupts = interrupts;
        Keypad = keypad;
        Cartridge = cartridge;
        _skipBios = skipBios;
    }

    public static GbaMachine Create(GbaMachineOptions options)
    {
        var memory = new GbaMemory();
        var interrupts = new InterruptController(memory);
        var keypad = new KeypadState(memory);
        var timers = new TimerController(interrupts, memory);
        var dma = new DmaController(interrupts, memory);
        var ppu = new Ppu(interrupts, dma, memory);
        var bus = new GbaBus(memory);
        var cpu = new Arm7Tdmi(bus, interrupts);

        var cartridge = options.RomPath is { Length: > 0 } romPath && File.Exists(romPath)
            ? GbaCartridge.Load(romPath)
            : null;

        bus.LoadCartridge(cartridge);
        bus.LoadBios(BiosImage.LoadOptional(options.BiosPath));

        var machine = new GbaMachine(cpu, bus, ppu, timers, dma, interrupts, keypad, cartridge, false);
        machine.Reset();
        return machine;
    }

    private void Reset() => Cpu.Reset(_skipBios);

    public void RunFrame() => RunCycles(Ppu.CyclesPerFrame);

    private void RunCycles(int cycles)
    {
        var iterations = 0;
        var consumed = 0;
        var cpuWatch = new Stopwatch();
        var dmaWatch = new Stopwatch();
        var timerWatch = new Stopwatch();
        var ppuWatch = new Stopwatch();
        while (consumed < cycles)
        {
            cpuWatch.Start();
            var instructionCycles = Cpu.Step();
            cpuWatch.Stop();
            dmaWatch.Start();
            Dma.RunDmas(DmaTimingType.Immediately, Bus);
            dmaWatch.Stop();
            timerWatch.Start();
            Timers.Step(instructionCycles);
            timerWatch.Stop();
            ppuWatch.Start();
            Ppu.Step(instructionCycles, Bus);
            ppuWatch.Stop();
            consumed += instructionCycles;
            iterations += 1;
        }
        Console.WriteLine($"{iterations} iterations completed");
        Console.WriteLine($"{cpuWatch.ElapsedMilliseconds} ms in CPU");
        Console.WriteLine($"{dmaWatch.ElapsedMilliseconds} ms in DMA");
        Console.WriteLine($"{timerWatch.ElapsedMilliseconds} ms in timers");
        Console.WriteLine($"{ppuWatch.ElapsedMilliseconds} ms in ppu");
    }
}
