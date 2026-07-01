using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format5AddCmpMovBxHiRegs
{
    /*
        2000000:       4478            add     r0, pc
        2000002:       44f8            add     r8, pc
       2000004:       44c8            add     r8, r9
        2000006:       4578            cmp     r0, pc
        2000008:       45f8            cmp     r8, pc
       200000a:       45c8            cmp     r8, r9
        200000c:       4678            mov     r0, pc
        200000e:       46f8            mov     r8, pc
       2000010:       46c8            mov     r8, r9
       2000012:       4778            bx      pc
       2000014:       4770            bx      lr
       2000016:       4700            bx      r0
     */

    [Fact]
    public void MOV_HiRegPcUnaligned_PcWordAlignedBeforeMov()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x08000102: mov r8, pc
        bus.Write16(0x08000102, 0x46f8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x08000102;
        cpu.Registers[8] = 0x0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x08000104u, cpu.Registers[8]);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void MOV_LoRegPcAligned_VisiblePcMovedFlagsUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x08000100: mov r0, pc
        bus.Write16(0x08000100, 0x4678);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x08000100;
        cpu.Registers[0] = 0x0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x08000104u, cpu.Registers[0]);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void CMP_PcEqualToLoRd_CarrySetHighNoBorrowZeroSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x08000100: cmp r8, pc
        bus.Write16(0x08000100, 0x45f8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x08000100;
        cpu.Registers[8] = 0x08000104;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x08000104u, cpu.Registers[8]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void CMP_PcLessThanLoRd_CarrySetLowForBorrowAndNegativeSetHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x08000100: cmp r0, pc
        bus.Write16(0x08000100, 0x4578);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x08000100;
        cpu.Registers[0] = 0x08000100;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x08000100u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Overflow);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void ADD_HiRegPcAligned_VisiblePcAddedToOperand()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x08000100: add r8, pc
        bus.Write16(0x08000100, 0x44f8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x08000100;
        cpu.Registers[8] = 0x20;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x08000124u, cpu.Registers[8]);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Negative);
    }

    [Fact]
    public void ADD_LoRegPcUnaligned_WordAlignsVisiblePc()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x08000102: add r0, pc
        bus.Write16(0x08000102, 0x4478);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x08000102;
        cpu.Registers[0] = 0x20;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x08000124u, cpu.Registers[0]);

        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);
        Assert.True(cpu.Cpsr.Negative);
    }
}