using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format5AddCmpMovBxHiRegs
{
    /*
       2000000:       4478            add     r0, pc
       2000002:       44f8            add     r8, pc
       2000004:       44c8            add     r8, r9
       2000006:       4578            cmp     r0, pc
       2000008:       45f8            cmp     r8, pc
       200000a:       45c8            cmp     r8, r9
       200000c:       4678            mov     r0, pc
       200000e:       46f8            mov     r8, pc
       2000010:       46c8            mov     r8, r9
       2000012:       4778            bx      pc
       2000014:       4770            bx      lr
       2000016:       4700            bx      r0
     */
}