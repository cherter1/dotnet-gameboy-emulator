using BenchmarkDotNet.Attributes;
using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Benchmarks;

[MemoryDiagnoser]
public class ArmAluBenchmarks
{
    private const int StepsPerInvoke = 16_384;
    private const uint RomBase = 0x08000000;

    private Arm7Tdmi _cpu = null!;
    private GbaBus _bus = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_cpu, _bus) = CpuUtilities.CreateCpu();
        _cpu.Reset(true);

        byte[] rom = new byte[StepsPerInvoke * sizeof(uint)];

        for (int offset = 0; offset < rom.Length; offset += sizeof(uint))
        {
            rom[offset + 0] = 0x31;
            rom[offset + 1] = 0x32;
            rom[offset + 2] = 0x10;
            rom[offset + 3] = 0xe0;
        }

        _bus.LoadCartridge(new Cartridge.Cartridge("benchmark.gba", rom));

        _cpu.Registers[0] = 0x10;
        _cpu.Registers[1] = 0x10;
        _cpu.Registers[2] = 1;
    }

    [Benchmark(OperationsPerInvoke = StepsPerInvoke)]
    public int StepArm_AndWithRotate()
    {
        _cpu.Registers.ProgramCounter = RomBase;
        int totalCycles = 0;

        for (int i = 0; i < StepsPerInvoke; i++)
        {
            totalCycles = _cpu.Step();
        }

        return totalCycles;
    }
}