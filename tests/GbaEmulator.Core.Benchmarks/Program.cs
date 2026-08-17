using BenchmarkDotNet.Running;

namespace GbaEmulator.Core.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<ArmSingleDataTransferBenchmarks>();
        //var armb =  new ArmSingleDataTransferBenchmarks();
        //armb.Setup();
        //armb.Opt_StepArm_Str();
        //Console.WriteLine(armb._storeBusOpt.Read32(0x03000000));
        //Console.WriteLine(armb._storeBusOpt.Read32(0x03000004));
        //Console.WriteLine(armb._storeBusOpt.Read32(0x03000008));
        //Console.WriteLine(armb._storeBusOpt.Read32(0x0300000c));
        //Console.WriteLine(armb._storeBusOpt.Read32(0x03000010));
    }
}