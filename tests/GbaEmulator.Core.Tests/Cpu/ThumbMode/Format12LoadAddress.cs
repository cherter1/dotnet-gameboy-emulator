using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format12LoadAddress
{
    /*
      2000000:       a000            add     r0, pc, #0      @ (adr r0, 2000004 <_start+0x4>)
      2000002:       a101            add     r1, pc, #4      @ (adr r1, 2000008 <_start+0x8>)
      2000004:       a7ff            add     r7, pc, #1020   @ (adr r7, 2000404 <_start+0x404>)
      2000006:       a800            add     r0, sp, #0
      2000008:       aa01            add     r2, sp, #4
      200000a:       adff            add     r5, sp, #1020   @ 0x3fc 
     */
    
    [Fact]
    public void ADD_PcWithZeroOffset_FlagsUnchangedAndR0HoldsVisiblePc()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, pc, #0
        bus.Write16(0x02000000, 0xa000);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000004u, cpu.Registers[0]);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_PcHalfwordAlignedWithZeroOffset_R0HoldsWordAlignedVisiblePc()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000002: add r0, pc, #0
        bus.Write16(0x02000002, 0xa000);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000002;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000004u, cpu.Registers[0]);
    }

    [Fact]
    public void ADD_PcWithFourOffset_R1HoldsVisiblePcPlusFour()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r1, pc, #4
        bus.Write16(0x02000000, 0xa101);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000008u, cpu.Registers[1]);
    }

    [Fact]
    public void ADD_PcWithMaxOffset1020_R7HoldsVisiblePcPlus1020()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r7, pc, #1020
        bus.Write16(0x02000000, 0xa7ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000400u, cpu.Registers[7]);
    }

    [Fact]
    public void ADD_PcHalfwordAlignedWithMaxOffset1020_R7HoldsVisiblePcWordAlignedPlus1020()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000002: add r7, pc, #1020
        bus.Write16(0x02000002, 0xa7ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000002;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000400u, cpu.Registers[7]);
    }

    [Fact]
    public void ADD_SpWithZeroOffset_R0HoldsSp()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, sp, #0
        bus.Write16(0x02000000, 0xa800);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007000u, cpu.Registers[0]);
    }

    [Fact]
    public void ADD_SpWithFourOffset_R2HoldsSpPlusFour()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r2, sp, #4
        bus.Write16(0x02000000, 0xaa01);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007004u, cpu.Registers[2]);
    }

    [Fact]
    public void ADD_SpWithMaxOffset1020_R5HoldsSpPlus1020()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r5, sp, #1020
        bus.Write16(0x02000000, 0xadff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x030073fcu, cpu.Registers[5]);
    }
}