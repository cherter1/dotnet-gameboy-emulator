using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ArmMode.DataProcessing;

public sealed class MovTests
{
    [Fact]
    public void LSLS_RegisterOperandsShiftedByOne_MovesShiftedValueIntoRegister()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: movs r0, r1, lsl r0
        // aka lsls r0, r1, r0
        bus.Write32(0x02000000, 0xe1b00011);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 1;
        cpu.Registers[1] = 0xFFF;
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x1FFEu, cpu.Registers[0]);
        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Theory]
    [InlineData(0x3, true)]
    [InlineData(0x2, false)]
    public void LSLS_RegisterOperandsShiftedBy32_ResultZeroIntoRegisterAndCarrySetOnBitZero(uint preshiftValue, bool expectedCarry)
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: movs r0, r1, lsl r0
        // aka lsls r0, r1, r0
        bus.Write32(0x02000000, 0xe1b00011);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 32;
        cpu.Registers[1] = preshiftValue;
        cpu.SetCarry(!expectedCarry);
        cpu.SetNegative(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0u, cpu.Registers[0]);
        Assert.Equal(expectedCarry, cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void LSLS_RegisterOperandsLeftShiftedByGreaterThan32_ResultZeroIntoRegisterAndCarryNotSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: movs r0, r1, lsl r0
        // aka lsls r0, r1, r0
        bus.Write32(0x02000000, 0xe1b00011);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 33;
        cpu.Registers[1] = 0x3;
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0u, cpu.Registers[0]);
        Assert.False(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Theory]
    [InlineData(0x80000fff, true)]
    [InlineData(0x70000fff, false)]
    public void LSRS_ImmediateOperandRightShift32_ResultZeroIntoRegisterAndCarryBecomes31Bit(uint preshiftValue, bool expectedCarry)
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: movs r0, r1, lsr #32
        // aka lsrs r0, r1, #32
        bus.Write32(0x02000000, 0xe1b00021);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = preshiftValue;
        cpu.SetNegative(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(!expectedCarry);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0u, cpu.Registers[0]);
        Assert.Equal(expectedCarry, cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
    }
}