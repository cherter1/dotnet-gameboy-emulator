using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format3AddSubMovCmp
{
    [Fact]
    public void MOV_MoveZeroIntoRegister_ZeroFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: mov r0, #0
        bus.Write16(0x02000000, 0x2000);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x1;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void MOV_MovesValueIntoRegister_ZeroAndNegativeFlagsCleared()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: mov r0, #1
        bus.Write16(0x02000000, 0x2001);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x1u, cpu.Registers[0]);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void MOV_MovesMaxValueIntoRegister_ZeroExtendsAndClearsNZFlags()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: mov r0, #255
        bus.Write16(0x02000000, 0x20ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffu, cpu.Registers[0]);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void CMP_ZeroToZero_ZAndCarryFlagSet() {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: cmp r0, #0
        bus.Write16(0x02000000, 0x2800);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);

        //Carry set high when didnt occur
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void CMP_ZeroToOne_RdUnchangedAndNegativeFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: cmp r0, #1
        bus.Write16(0x02000000, 0x2801);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void CMP_Compare255ToMaxImm_RdUnchangedAndZCFlagsSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: cmp r0, #255
        bus.Write16(0x02000000, 0x28ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xFF;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_ZeroToZero_ZFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, #0
        bus.Write16(0x02000000, 0x3000);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_SimpleAddition_NoFlagsSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, #1
        bus.Write16(0x02000000, 0x3001);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xe;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xfu, cpu.Registers[0]);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }
    
    [Fact]
    public void ADD_MaxPlusOne_CarryAndZeroSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, #1
        bus.Write16(0x02000000, 0x3001);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xffffffff;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ADD_MaxPositivePlusOne_OverFlowAndNegativeSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, #1
        bus.Write16(0x02000000, 0x3001);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x7fffffff;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x80000000u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Carry);
    }

    [Fact]
    public void ADD_NegativePlusMaxImm_NegativeSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: add r0, #255
        bus.Write16(0x02000000, 0x30ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x800000ffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Carry);
    }

    [Fact]
    public void SUB_ZeroMinusZero_CarrySetAndZeroSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub r0, #0
        bus.Write16(0x02000000, 0x3800);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void SUB_SimpleSubtraction_CarrySetHighRegIsSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub r0, #1
        bus.Write16(0x02000000, 0x3801);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x10;
        cpu.SetThumbState(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xfu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void SUB_ZeroMinusOne_NegativeFlagSetCarryCleared()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub r0, #1
        bus.Write16(0x02000000, 0x3801);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffffffffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void SUB_ImmGreaterThanPositiveRegValue_NegativeFlagSetCarryCleared()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub r0, #255
        bus.Write16(0x02000000, 0x38ff);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xfe;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffffffffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void SUB_MinNegativeRegValueMinusOne_OverFlowAndCarrySet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sub r0, #1
        bus.Write16(0x02000000, 0x3801);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000000;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x7fffffffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
    }
}