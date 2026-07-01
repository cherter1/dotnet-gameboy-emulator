using GbaEmulator.Core.Cpu;
using GbaEmulator.Core.Memory;
using GbaEmulator.Core.Tests.TestUtils;

namespace GbaEmulator.Core.Tests.Cpu.ThumbMode;

public sealed class Format4AluOps
{
    /*
     temp instruction comment
      2000000:       4008            ands    r0, r1
      2000002:       4048            eors    r0, r1
      2000004:       4088            lsls    r0, r1
      2000006:       40c8            lsrs    r0, r1
      2000008:       4108            asrs    r0, r1
      200000a:       4148            adcs    r0, r1
      200000c:       4188            sbcs    r0, r1
      200000e:       41c8            rors    r0, r1
      2000010:       4208            tst     r0, r1
      2000012:       4248            negs    r0, r1
      2000014:       4288            cmp     r0, r1
      2000016:       42c8            cmn     r0, r1
      2000018:       4308            orrs    r0, r1
      200001a:       4348            muls    r0, r1
      200001c:       4388            bics    r0, r1
      200001e:       43c8            mvns    r0, r1 
     */
    [Fact]
    public void AND_OperandsDontShareAnyHighBits_ZeroSetCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: and r0, r1
        bus.Write16(0x02000000, 0x4008);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xf0;
        cpu.Registers[1] = 0x0f;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);

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
    public void EOR_RightOperandHasBottom4BitsUnset_ResultHasFAndCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: eor r0, r1
        bus.Write16(0x02000000, 0x4048);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xff;
        cpu.Registers[1] = 0xf0;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xfu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void LSL_30BitSetShiftsOne_VUnchangedNegativeSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: lsl r0, r1
        bus.Write16(0x02000000, 0x4088);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x40000000;
        cpu.Registers[1] = 1;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x80000000u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void LSL_ZeroShift_RegisterUnchangedCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: lsl r0, r1
        bus.Write16(0x02000000, 0x4088);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x12345678;
        cpu.Registers[1] = 0;
        cpu.SetThumbState(true);
        cpu.SetCarry(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x12345678u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void LSR_ZeroBitSetShiftOne_VUnchangedCarrySet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: lsr r0, r1
        bus.Write16(0x02000000, 0x40c8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000001;
        cpu.Registers[1] = 1;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x40000000u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void LSR_31BitSetAndShift32_ZeroResultCarrySet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: lsr r0, r1
        bus.Write16(0x02000000, 0x40c8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000000;
        cpu.Registers[1] = 32;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void ASR_NegativeShiftOne_CarryClearedVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: asr r0, r1
        bus.Write16(0x02000000, 0x4108);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000000;
        cpu.Registers[1] = 1;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xc0000000u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void ASR_NegativeShift32Plus_SignExtendedCarryFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: asr r0, r1
        bus.Write16(0x02000000, 0x4108);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000000;
        cpu.Registers[1] = 32;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffffffffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Negative);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void ASR_PositiveShift32Plus_SignExtendedCarryCleared()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: asr r0, r1
        bus.Write16(0x02000000, 0x4108);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x40000000;
        cpu.Registers[1] = 32;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void ADC_AddZeroWithCarry_ZeroResultSetsCarry()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: adc r0, r1
        bus.Write16(0x02000000, 0x4148);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xffffffff;
        cpu.Registers[1] = 0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);

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
    public void SBC_LeftGreaterThanRightCarrySetHigh_NoBorrow()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sbc r0, r1
        bus.Write16(0x02000000, 0x4188);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 5;
        cpu.Registers[1] = 3;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(2u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void SBC_LeftGreaterThanRightCarrySetLow_ResultUsesBorrowAndCarrySetBackHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: sbc r0, r1
        bus.Write16(0x02000000, 0x4188);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 5;
        cpu.Registers[1] = 3;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(1u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ROR_NegativeNumberLastBitOutSetHigh_NegativeNumberCarrySetHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: ror r0, r1
        bus.Write16(0x02000000, 0x41c8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x80000001;
        cpu.Registers[1] = 1;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xc0000000u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Zero);
    }

    [Fact]
    public void TST_OperandsHaveNoSharedBits_ZeroSetHighCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: tst r0, r1
        bus.Write16(0x02000000, 0x4208);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x0f;
        cpu.Registers[1] = 0xf0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0fu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void NEG_SourceRegOne_CarrySetLowNegativeSetHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: neg r0, r1
        bus.Write16(0x02000000, 0x4248);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x0f;
        cpu.Registers[1] = 1;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetZero(true);
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
    public void NEG_SourceRegZero_NoBorrowZeroSetHigh()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: neg r0, r1
        bus.Write16(0x02000000, 0x4248);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x0f;
        cpu.Registers[1] = 0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);

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
    public void CMP_RightGreaterThanLeftOperand_NegativeFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: cmp r0, r1
        bus.Write16(0x02000000, 0x4288);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 3;
        cpu.Registers[1] = 5;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x3u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void CMN_MaxPlusOne_ZeroAndCarrySet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: cmn r0, r1
        bus.Write16(0x02000000, 0x42c8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xffffffff;
        cpu.Registers[1] = 1;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffffffffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Negative);
        Assert.False(cpu.Cpsr.Overflow);
    }

    [Fact]
    public void ORR_BitsOtherThanTopBitSet_NZSetLowCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: orr r0, r1
        bus.Write16(0x02000000, 0x4308);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0x0f;
        cpu.Registers[1] = 0xf0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Overflow);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void MUL_NoBorrowMultiply_CDestroyedVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: mul r0, r1
        bus.Write16(0x02000000, 0x4348);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 3;
        cpu.Registers[1] = 7;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x15u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void MUL_MultiplyZero_ZeroFlagSet()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: mul r0, r1
        bus.Write16(0x02000000, 0x4348);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 3;
        cpu.Registers[1] = 0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetNegative(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x00u, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Zero);

        Assert.False(cpu.Cpsr.Carry);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void BIC_BottomFourBitsSwapped_CVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: bic r0, r1
        bus.Write16(0x02000000, 0x4388);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xff;
        cpu.Registers[1] = 0xf0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetNegative(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0x0fu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Carry);

        Assert.False(cpu.Cpsr.Zero);
        Assert.False(cpu.Cpsr.Negative);
    }

    [Fact]
    public void MVN_MoveZero_MaxValueResultCVUnchanged()
    {
        //Arrange
        (Arm7Tdmi cpu, GbaBus bus) = CpuUtilities.CreateCpu();

        // 0x02000000: mvn r0, r1
        bus.Write16(0x02000000, 0x43c8);

        cpu.Reset(true);
        cpu.Registers.ProgramCounter = 0x02000000;
        cpu.Registers[0] = 0xff;
        cpu.Registers[1] = 0x0;
        cpu.SetThumbState(true);
        cpu.SetOverflow(true);
        cpu.SetCarry(true);
        cpu.SetZero(true);

        //Act
        cpu.Step();

        //Assert
        Assert.Equal(0xffffffffu, cpu.Registers[0]);
        Assert.True(cpu.Cpsr.Overflow);
        Assert.True(cpu.Cpsr.Carry);
        Assert.True(cpu.Cpsr.Negative);

        Assert.False(cpu.Cpsr.Zero);
    }
}