using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format15MultipleLoadStore
{
    [Fact]
    public void STMIA_ThreeSparseRegisters_FlagsUnchangedAndRegistersValuePushedToIncrementingR4Address()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: stmia r4!, {r0, r3, r7}
        bus.Write16(0x02000000, 0xc489);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[4] = 0x02000010;
        cpu.Registers[0] = 0xaaaabbbb;
        cpu.Registers[3] = 0xccccdddd;
        cpu.Registers[7] = 0xeeeeffff;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0200001cu, cpu.Registers[4]);
        Assert.Equal(0xaaaabbbb, bus.Read32(0x02000010));
        Assert.Equal(0xccccdddd, bus.Read32(0x02000014));
        Assert.Equal(0xeeeeffff, bus.Read32(0x02000018));

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);
    }

    [Fact]
    public void STMIA_BaseInListFirst_BaseAddressStoresOriginalBaseValueAndWriteback()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: stmia r0!, {r0, r1}
        bus.Write16(0x02000000, 0xc003);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x02000010;
        cpu.Registers[1] = 0xccccdddd;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000018u, cpu.Registers[0]);
        Assert.Equal(0x02000010u, bus.Read32(0x02000010));
        Assert.Equal(0xccccddddu, bus.Read32(0x02000014));
    }

    [Fact]
    public void STMIA_BaseInListNotFirst_BaseAddressStoresNewBaseValueAndWriteback()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: stmia r1!, {r0, r1}
        bus.Write16(0x02000000, 0xc103);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xaaaabbbb;
        cpu.Registers[1] = 0x02000010;
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x02000018u, cpu.Registers[1]);
        Assert.Equal(0xaaaabbbbu, bus.Read32(0x02000010));
        Assert.Equal(0x02000018u, bus.Read32(0x02000014));
    }

    [Fact]
    public void LDMIA_ThreeSpareRegisters_ValuesLoadedIntoRegisterAndBaseIncrementedAndWrittenbackFlagsUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: ldmia r4!, {r0, r3, r7}
        bus.Write16(0x02000000, 0xcc89);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[4] = 0x02000010;
        bus.Write32(0x02000010, 0xaaaabbbb);
        bus.Write32(0x02000014, 0xccccdddd);
        bus.Write32(0x02000018, 0xeeeeffff);

        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0200001cu, cpu.Registers[4]);
        Assert.Equal(0xaaaabbbbu, cpu.Registers[0]);
        Assert.Equal(0xccccddddu, cpu.Registers[3]);
        Assert.Equal(0xeeeeffffu, cpu.Registers[7]);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void LDMIA_BaseInListFirst_WritebackNotRetainedOverwrittenByLoad()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: ldmia r0!, {r0, r1}
        bus.Write16(0x02000000, 0xc803);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x02000010;
        bus.Write32(0x02000010, 0xaaaabbbb);
        bus.Write32(0x02000014, 0xccccdddd);
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xaaaabbbb, cpu.Registers[0]);
        Assert.Equal(0xccccdddd, cpu.Registers[1]);
    }

    [Fact]
    public void LDMIA_BaseInListNotFirst_WritebackNotRetainedOverwrittenByLoad()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: ldmia r1!, {r0, r1}
        bus.Write16(0x02000000, 0xc903);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[1] = 0x02000010;
        bus.Write32(0x02000010, 0xaaaabbbb);
        bus.Write32(0x02000014, 0xccccdddd);
        cpu.SetThumbState(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xaaaabbbb, cpu.Registers[0]);
        Assert.Equal(0xccccdddd, cpu.Registers[1]);
    }
}