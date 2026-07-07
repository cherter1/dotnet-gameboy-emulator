using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format17SoftwareInterrupt
{
    [Fact]
    public void SWI_SystemModeAllFlagsSet_SupervisorSwitchPcSetToVectorSpsrSetToOldCpsrAndLrSvcSetToOldPc()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000100: swi #0
        bus.Write16(0x02000100, 0xdf00);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000100;
        cpu.SetThumbState(true);
        cpu.SetZero(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x00000008u, cpu.Registers.ProgramCounter);
        Assert.Equal(0x02000102u, cpu.Registers.LinkRegister);
        Assert.False(cpu.Cpsr.ThumbState);

        var oldCpsr = cpu.Registers.GetSpsr(CpuMode.Supervisor);
        Assert.True(oldCpsr.ThumbState);
        Assert.True(oldCpsr.Zero);
        Assert.True(oldCpsr.Overflow);
        Assert.True(oldCpsr.Negative);
        Assert.True(oldCpsr.Carry);
    }
}