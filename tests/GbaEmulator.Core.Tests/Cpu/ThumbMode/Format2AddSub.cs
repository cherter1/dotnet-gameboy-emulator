using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format2AddSub
{
    [Fact]
    public void ADD_SimpleAdd_NoFlagsSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #1
        bus.Write16(0x02000000, 0x1c48);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x0000000e;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0000000fu, cpu.Registers[0]);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Carry);
    }

    [Fact]
    public void ADD_AddPositiveNumberToZero_ValueUnchangedAndFlagsSetLow()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #0
        bus.Write16(0x02000000, 0x1c08);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x12345678;
        cpu.SetThumbState(true);

        cpu.SetCarry(true);
        cpu.SetZero(true);
        cpu.SetNegative(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x12345678u, cpu.Registers[0]);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Carry);
    }

    [Fact]
    public void ADD_AddZeroToZero_ZeroFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #0
        bus.Write16(0x02000000, 0x1c08);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x0;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Zero);
    }

    [Fact]
    public void ADD_ResultHas31BitSet_OverflowFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #1
        bus.Write16(0x02000000, 0x1c48);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x7fffffff;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x80000000u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Negative);
    }

    [Fact]
    public void ADD_ValueWrapsToZero_ZeroAndCarryFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #1
        bus.Write16(0x02000000, 0x1c48);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0xffffffff;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
    }

    [Fact]
    public void ADD_UnsignedCarryWithNonZeroResult_CarryFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #7
        bus.Write16(0x02000000, 0x1dc8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0xfffffffe;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x5u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_MaxImmNoCarry_NoFlagsSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #7
        bus.Write16(0x02000000, 0x1dc8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x8;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xfu, cpu.Registers[0]);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_MaxImmToNegativeNumber_NoOverFlowAndNegativeFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, r1, #7
        bus.Write16(0x02000000, 0x1dc8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x80000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x80000007u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
    }
}