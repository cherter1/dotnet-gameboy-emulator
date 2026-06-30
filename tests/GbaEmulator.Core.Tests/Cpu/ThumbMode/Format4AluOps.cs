using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format4AluOps
{
    /*
     temp instruction comment
      2000000:       4008            ands    r0, r1
      2000002:       4048            eors    r0, r1
      2000004:       4088            lsls    r0, r1
      2000006:       40c8            lsrs    r0, r1
      2000008:       4108            asrs    r0, r1
      200000a:       4148            adcs    r0, r1
      200000c:       4188            sbcs    r0, r1
      200000e:       41c8            rors    r0, r1
      2000010:       4208            tst     r0, r1
      2000012:       4248            negs    r0, r1
      2000014:       4288            cmp     r0, r1
      2000016:       42c8            cmn     r0, r1
      2000018:       4308            orrs    r0, r1
      200001a:       4348            muls    r0, r1
      200001c:       4388            bics    r0, r1
      200001e:       43c8            mvns    r0, r1 
     */
    [Fact]
    public void AND_OperandsDontShareAnyHighBits_ZeroSetCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: and r0, r1
        bus.Write16(0x02000000, 0x4008);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xf0;
        cpu.Registers[1] = 0x0f;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Negative);
    }
}