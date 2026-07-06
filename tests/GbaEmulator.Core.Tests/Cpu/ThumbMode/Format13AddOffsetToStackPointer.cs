using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format13AddOffsetToStackPointer
{
    [Fact]
    public void ADD_ZeroOffset_NoOpAndFlagsUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add sp, #0
        bus.Write16(0x02000000, 0xb000);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007000u, cpu.Registers.StackPointer);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_FourOffset_StackPointerUpdatedPlusFour()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add sp, #4
        bus.Write16(0x02000000, 0xb001);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007004u, cpu.Registers.StackPointer);
    }

    [Fact]
    public void ADD_MaxOffset508_StackPointerIncremented508()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add sp, #508
        bus.Write16(0x02000000, 0xb07f);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x030071fcu, cpu.Registers.StackPointer);
    }

    [Fact]
    public void SUB_ZeroOffset_StackPointerUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub sp, #0
        bus.Write16(0x02000000, 0xb080);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007000u, cpu.Registers.StackPointer);
    }

    [Fact]
    public void SUB_FourOffset_StackPointerDecrementedFour()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub sp, #4
        bus.Write16(0x02000000, 0xb081);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x03007004;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007000u, cpu.Registers.StackPointer);
    }

    [Fact]
    public void SUB_MaxOffset_StackPointerDecremented508()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub sp, #508
        bus.Write16(0x02000000, 0xb0ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[13] = 0x030071fc;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x03007000u, cpu.Registers.StackPointer);
    }
}