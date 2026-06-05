namespace GbaEmulator.Core.Common;

public static class DebugUtilities
{
    public static void DumpTrace(CpuTrace?[] traces, ref int traceIndex)
    {
        using var writer = new StreamWriter("../../../../CpuTraceDump.txt");
        for (int i = 0; i < traces.Length; i++)
        {
            var index = (traceIndex + i) % traces.Length;
            var trace = traces[index];
            if (trace is null)
                continue;
            var isThumbInstruction = trace.RawInstruction >> 16 == 0;
            var rawInstructionToPrint = isThumbInstruction ? trace.RawInstruction.ToString("X4") : trace.RawInstruction.ToString("X8");

            writer.WriteLine(
                $"[{index}] " +
                $"{trace.InstructionAddress:X8} " +
                $"{(trace.ThumbState ? "THUMB" : "ARM  ")} " +
                $"{trace.Mode, -3} " +
                $"instruction={rawInstructionToPrint} " +
                $"pcBefore={trace.PcBefore:X8} pcAfter={trace.PcAfter:X8} " +
                $"lr={trace.Lr:X8} sp={trace.Sp:X8} cpsr={trace.Cpsr:X8} " +
                $"{trace.Decoded}"
                );
        }
    }

    public static void AddTrace(CpuTrace?[] traces, CpuTrace newTrace, ref int traceIndex)
    {
        traces[traceIndex] = newTrace;
        traceIndex = (traceIndex + 1) % traces.Length;
    }
}