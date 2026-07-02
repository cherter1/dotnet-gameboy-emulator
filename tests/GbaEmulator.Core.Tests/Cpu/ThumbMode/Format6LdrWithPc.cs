using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format6LdrWithPc
{
    /*
       2000000:       4800            ldr     r0, [pc, #0]    @ (2000004 <_start+0x4>)
      2000002:       4901            ldr     r1, [pc, #4]    @ (2000008 <_start+0x8>)
      2000004:       4a03            ldr     r2, [pc, #12]   @ (2000014 <_start+0x14>)
      2000006:       4bff            ldr     r3, [pc, #1020] @ (2000404 <_start+0x404>)
      2000008:       4cff            ldr     r4, [pc, #1020] @ (2000408 <_start+0x408>)
      200000a:       4f02            ldr     r7, [pc, #8]    @ (2000014 <_start+0x14>)
      200000c:       4d01            ldr     r5, [pc, #4]    @ (2000014 <_start+0x14>) 
     */
    [Fact]
    public void LDR__Temp()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: ldr r0 [pc, #0]
        bus.Write16(0x02000100, 0x4800);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[14] = 0x02000201;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000200u, cpu.Registers.ProgramCounter);
        Assert.True(cpu.Cpsr.ThumbState);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
    }
}