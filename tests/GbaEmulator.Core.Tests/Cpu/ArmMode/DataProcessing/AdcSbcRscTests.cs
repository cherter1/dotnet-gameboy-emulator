using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ArmMode.DataProcessing;

public sealed class AdcSbcRscTests
{
    /*
        1c:   e0b10002        adcs    r0, r1, r2
       20:   e0d10002        sbcs    r0, r1, r2
       24:   e0f10002        rscs    r0, r1, r2 
     */

    [Fact]
    public void ADCS__test()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: adcs r0, r1, r2
        bus.Write32(0x02000000, 0xE0b10002);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x1;
        cpu.Registers[1] = 0;
        cpu.Registers[2] = 0xFFFFFFFF;
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void SBCS__test()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sbcs r0, r1, r2
        bus.Write32(0x02000000, 0xE0d10002);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x1;
        cpu.Registers[1] = 0x7fffffff;
        cpu.Registers[2] = 0;

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x7ffffffeu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }
}