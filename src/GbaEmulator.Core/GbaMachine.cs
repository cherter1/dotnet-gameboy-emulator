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
        var interrupts = new InterruptController();
        var keypad = new KeypadState();
        var timers = new TimerController(interrupts);
        var dma = new DmaController(interrupts);
        var ppu = new Ppu(interrupts, dma);
        var bus = new GbaBus(interrupts, timers, dma, ppu, keypad);
        var cpu = new Arm7Tdmi(bus, interrupts);

        var cartridge = options.RomPath is { Length: > 0 } romPath && File.Exists(romPath)
            ? GbaCartridge.Load(romPath)
            : null;

        bus.LoadCartridge(cartridge);
        bus.LoadBios(BiosImage.LoadOptional(options.BiosPath));

        var machine = new GbaMachine(cpu, bus, ppu, timers, dma, interrupts, keypad, cartridge, false); // options.SkipBios);
        machine.Reset();
        return machine;
    }

    public void Reset() => Cpu.Reset(_skipBios);

    public void RunFrame() => RunCycles(Ppu.CyclesPerFrame);

    public void RunCycles(int cycles)
    {
        var consumed = 0;
        while (consumed < cycles)
        {
            var instructionCycles = Cpu.Step();
            Timers.Step(instructionCycles);
            Ppu.Step(instructionCycles, Bus);
            consumed += instructionCycles;
        }
    }
}
