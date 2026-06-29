using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format1Shifts
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void LSL_OffsetIsZero_NZFlagsSetLowAndVCFlagsUnchanged(bool initialFlagValue)
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();
        
        // 0x02000000: lsl r0, r1, #0
        bus.Write16(0x02000000, 0x0008);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x12345678;
        cpu.SetThumbState(true);
        
        // set flags high
        cpu.SetCarry(initialFlagValue);
        cpu.SetOverflow(initialFlagValue);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x12345678u, cpu.Registers[0]);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
        
        Assert.Equal(initialFlagValue, cpu.Cpsr.Overflow);
        Assert.Equal(initialFlagValue, cpu.Cpsr.Carry);
    }
    
    [Fact]
    public void LSL_NormalShift_CarryGetsOld31Bit()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();
        
        // 0x02000000: lsl r0, r1, #1
        bus.Write16(0x02000000, 0x0048);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x80000001;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x2u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void LSL_ResultBecomesNegative_NFlagGetsSetHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();
        
        // 0x02000000: lsl r0, r1, #1
        bus.Write16(0x02000000, 0x0048);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x40000000;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x80000000, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Zero);
    }
    
    [Fact]
    public void LSL_ZeroResult_CarryClearedAndZeroFlagSetHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();
        
        // 0x02000000: lsl r0, r1, #31
        bus.Write16(0x02000000, 0x07c8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x10;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Negative);
    }
}