using System.Runtime.CompilerServices;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using GbaEmulator.Core.Cpu;

namespace GbaEmulator.Core.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ArmAluBenchmarks>();
    }
}