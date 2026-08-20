using BenchmarkDotNet.Attributes;
using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Cpu.TempOptimize;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Benchmarks.Cpu;

[MemoryDiagnoser]
[SimpleJob(launchCount: 3)]
[MinIterationTime(250)]
[MaxRelativeError(0.005)]
[StatisticalTestColumn("2%")]
public class ArmSingleDataTransferBenchmarks
{
    private const int StepsPerInvoke = 16_384;
    private const uint RomBase = 0x08000000;

    public Arm7Tdmi _loadCpu = null!;
    public GbaBus _loadBus = null!;
    public Arm7Tdmi _storeCpu = null!;
    public GbaBus _storeBus = null!;

    public CpuOpt _loadCpuOpt = null!;
    public BusOpt _loadBusOpt = null!;
    public CpuOpt _storeCpuOpt = null!;
    public BusOpt _storeBusOpt = null!;

    [GlobalSetup]
    public void Setup()
    {
        (_loadCpu, _loadBus) = CpuUtilities.CreateCpu();
        (_storeCpu, _storeBus) = CpuUtilities.CreateCpu();
        (_loadCpuOpt, _loadBusOpt) = CpuUtilities.CreateCpuOpt();
        (_storeCpuOpt, _storeBusOpt) = CpuUtilities.CreateCpuOpt();
        _loadCpu.Reset(true);
        _storeCpu.Reset(true);
        _loadCpuOpt.Reset(true);
        _storeCpuOpt.Reset(true);

        byte[] rom = new byte[StepsPerInvoke * sizeof(uint)];
        byte[] rom2 = new byte[StepsPerInvoke * sizeof(uint)];

        for (int offset = 0; offset < rom.Length; offset += sizeof(uint))
        {
            rom[offset + 0] = 0xa2;
            rom[offset + 1] = 0x00;
            rom[offset + 2] = 0x91;
            rom[offset + 3] = 0xe7;

            rom2[offset + 0] = 0xa2;
            rom2[offset + 1] = 0x00;
            rom2[offset + 2] = 0x81;
            rom2[offset + 3] = 0xe7;
        }

        _loadBus.LoadCartridge(new Cartridge.Cartridge("load.gba", rom));
        _storeBus.LoadCartridge(new Cartridge.Cartridge("store.gba", rom2));

        _loadBusOpt.LoadCartridge(new Cartridge.Cartridge("loadOpt.gba", rom));
        _storeBusOpt.LoadCartridge(new Cartridge.Cartridge("storeOpt.gba", rom2));

        _loadCpu.Registers[0] = 0x10;
        _loadCpu.Registers[1] = 0x03000000;
        _loadCpu.Registers[2] = 0x8;

        _loadCpuOpt.Registers[0] = 0x10;
        _loadCpuOpt.Registers[1] = 0x03000000;
        _loadCpuOpt.Registers[2] = 0x8;

        _storeCpu.Registers[0] = 0x11abcdef;
        _storeCpu.Registers[1] = 0x03000000;
        _storeCpu.Registers[2] = 0x8;

        _storeCpuOpt.Registers[0] = 0x11abcdef;
        _storeCpuOpt.Registers[1] = 0x03000000;
        _storeCpuOpt.Registers[2] = 0x8;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = StepsPerInvoke)]
    public int StepArm_Ldr()
    {
        _loadCpu.Registers.ProgramCounter = RomBase;
        int totalCycles = 0;

        for (int i = 0; i < StepsPerInvoke; i++)
        {
            totalCycles = _loadCpu.Step();
        }

        return totalCycles;
    }

    //[Benchmark(Baseline = true, OperationsPerInvoke = StepsPerInvoke)]
    public int StepArm_Str()
    {
        _storeCpu.Registers.ProgramCounter = RomBase;
        int totalCycles = 0;

        for (int i = 0; i < StepsPerInvoke; i++)
        {
            totalCycles = _storeCpu.Step();
        }

        return totalCycles;
    }

    [Benchmark(OperationsPerInvoke = StepsPerInvoke)]
    public int Opt_StepArm_Ldr()
    {
        _loadCpuOpt.Registers.ProgramCounter = RomBase;
        int totalCycles = 0;

        for (int i = 0; i < StepsPerInvoke; i++)
        {
            totalCycles = _loadCpuOpt.Step();
        }

        return totalCycles;
    }

    //[Benchmark(OperationsPerInvoke = StepsPerInvoke)]
    public int Opt_StepArm_Str()
    {
        _storeCpuOpt.Registers.ProgramCounter = RomBase;
        int totalCycles = 0;

        for (int i = 0; i < StepsPerInvoke; i++)
        {
            totalCycles = _storeCpuOpt.Step();
        }

        return totalCycles;
    }
}