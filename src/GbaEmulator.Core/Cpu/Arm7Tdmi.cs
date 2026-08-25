using System.Diagnostics;
using System.Runtime.CompilerServices;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Cpu;

public sealed partial class Arm7Tdmi(GbaBus bus, InterruptController interrupts)
{
    private readonly CpuTrace?[] _traces = new CpuTrace?[1024];
    private int _traceIndex;
    public RegisterBank Registers { get; private set; } = null!;

    public void Reset(bool skipBios)
    {
        Registers = new RegisterBank();
        Registers.InitializeForGba();

        Registers.Cpsr = new ProgramStatusRegister
        {
            Mode = CpuMode.System,
            IrqDisable = false,
            ThumbState = false
        };

        Registers.ProgramCounter = skipBios ? 0x08000000u : 0u;
    }

    public void SetThumbState(bool enabled) =>
        Registers.Cpsr = ProgramStatusRegister.FromUInt32(BitUtils.SetBit(Registers.Cpsr.ToUInt32(), 5, enabled));
    private int _cycles;

    public int Step()
    {
        try
        {
            if (!Registers.Cpsr.IrqDisable && interrupts.ServiceIrq)
            {
                //Console.WriteLine("Exception Entered");
                EnterIrqException();
                return 4;
            }

#if DEBUG
            if (Registers.ProgramCounter % 2 == 1)
            {
                DebugUtilities.DumpTrace(_traces, ref _traceIndex);
                Console.WriteLine(nameof(ArmBranch) + $": {ArmBranch:N0}");
                Console.WriteLine(nameof(ArmBlockDataTransfer) + $": {ArmBlockDataTransfer:N0}");
                Console.WriteLine(nameof(ArmSingleDataTransfer) + $": {ArmSingleDataTransfer:N0}");
                Console.WriteLine(nameof(ArmSwi) + $": {ArmSwi:N0}");
                Console.WriteLine(nameof(ArmBranchExchange) + $": {ArmBranchExchange:N0}");
                Console.WriteLine(nameof(ArmSingleDataSwap) + $": {ArmSingleDataSwap:N0}");
                Console.WriteLine(nameof(ArmMultiply) + $": {ArmMultiply:N0}");
                Console.WriteLine(nameof(ArmMultiplyLong) + $": {ArmMultiplyLong:N0}");
                Console.WriteLine(nameof(ArmHalfwordSignedDataTransfer) + $": {ArmHalfwordSignedDataTransfer:N0}");
                Console.WriteLine(nameof(ArmMrs) + $": {ArmMrs:N0}");
                Console.WriteLine(nameof(ArmMsr) + $": {ArmMsr:N0}");
                Console.WriteLine(nameof(ArmDataProc) + $": {ArmDataProc:N0}");
                Console.WriteLine("THUMB");
                Console.WriteLine(nameof(ThumbFormat1) + $": {ThumbFormat1:N0}");
                Console.WriteLine(nameof(ThumbFormat2) + $": {ThumbFormat2:N0}");
                Console.WriteLine(nameof(ThumbFormat3) + $": {ThumbFormat3:N0}");
                Console.WriteLine(nameof(ThumbFormat4) + $": {ThumbFormat4:N0}");
                Console.WriteLine(nameof(ThumbFormat5) + $": {ThumbFormat5:N0}");
                Console.WriteLine(nameof(ThumbFormat6) + $": {ThumbFormat6:N0}");
                Console.WriteLine(nameof(ThumbFormat7) + $": {ThumbFormat7:N0}");
                Console.WriteLine(nameof(ThumbFormat8) + $": {ThumbFormat8:N0}");
                Console.WriteLine(nameof(ThumbFormat9) + $": {ThumbFormat9:N0}");
                Console.WriteLine(nameof(ThumbFormat10) + $": {ThumbFormat10:N0}");
                Console.WriteLine(nameof(ThumbFormat11) + $": {ThumbFormat11:N0}");
                Console.WriteLine(nameof(ThumbFormat12) + $": {ThumbFormat12:N0}");
                Console.WriteLine(nameof(ThumbFormat13) + $": {ThumbFormat13:N0}");
                Console.WriteLine(nameof(ThumbFormat14) + $": {ThumbFormat14:N0}");
                Console.WriteLine(nameof(ThumbFormat15) + $": {ThumbFormat15:N0}");
                Console.WriteLine(nameof(ThumbFormat16) + $": {ThumbFormat16:N0}");
                Console.WriteLine(nameof(ThumbFormat17) + $": {ThumbFormat17:N0}");
                Console.WriteLine(nameof(ThumbFormat18) + $": {ThumbFormat18:N0}");
                Console.WriteLine(nameof(ThumbFormat19) + $": {ThumbFormat19:N0}");
            }
#endif

            _cycles = 0;
            if (Registers.Cpsr.ThumbState)
            {
                StepThumb();
            }
            else
            {
                StepArm();
            }

            return _cycles;
        }
        catch (Exception)
        {
            //DebugUtilities.DumpTrace(_traces, ref _traceIndex);
            throw;
        }
    }

#region DEBUG
    private int ArmBranch = 0;
    private int ArmBlockDataTransfer = 0;
    private int ArmSingleDataTransfer = 0;
    private int ArmSwi = 0;
    private int ArmBranchExchange = 0;
    private int ArmSingleDataSwap = 0;
    private int ArmMultiply = 0;
    private int ArmMultiplyLong = 0;
    private int ArmHalfwordSignedDataTransfer = 0;
    private int ArmMrs = 0;
    private int ArmMsr = 0;
    private int ArmDataProc = 0;
    private int ThumbFormat1 = 0;
    private int ThumbFormat2 = 0;
    private int ThumbFormat3 = 0;
    private int ThumbFormat4 = 0;
    private int ThumbFormat5 = 0;
    private int ThumbFormat6 = 0;
    private int ThumbFormat7 = 0;
    private int ThumbFormat8 = 0;
    private int ThumbFormat9 = 0;
    private int ThumbFormat10 = 0;
    private int ThumbFormat11 = 0;
    private int ThumbFormat12 = 0;
    private int ThumbFormat13 = 0;
    private int ThumbFormat14 = 0;
    private int ThumbFormat15 = 0;
    private int ThumbFormat16 = 0;
    private int ThumbFormat17 = 0;
    private int ThumbFormat18 = 0;
    private int ThumbFormat19 = 0;
#endregion

    private void StepArm()
    {
        var instructionAddress = Registers.ProgramCounter;

        var instruction = bus.Read32(instructionAddress);
        Registers.ProgramCounter = instructionAddress + 4;

        var pcBeforeExecute = Registers.ProgramCounter;
        var decoded = "UNKNOWN";
        try
        {
            if (!ConditionPassed((Condition)(instruction >> 28))) //bits 31-28
            {
                decoded = $"COND FAILED {(Condition)(instruction >> 28)}";
                if (instruction == 0x00000000)
                {
                    //throw new Exception();
                }
                _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true);
                return;
            }

            var bits27_25 = (instruction >> 25) & 0b111;

            if (bits27_25 == 0b101)
            {
                // B, BL
                decoded = BitUtils.IsBitSet(instruction, 24) ? "BL" : "B";
                ExecuteArmBranch(instruction);
                ArmBranch++;
                return;
            }

            if (bits27_25 == 0b100)
            {
                // LDM, STM
                decoded = "LDM/STM";
                ArmBlockDataTransfer++;
                ExecuteBlockDataTransfer(instruction);
                return;
            }

            // 0000_1100_0001_0000_0000_0000_0000_0000 == 0000_0100_0001_0000_0000_0000_0000_0000
            if ((instruction & 0xc100000) == 0x4100000) //bit 20 set is load
            {
                // LDR
                decoded = "LDR";
                ArmSingleDataTransfer++;
                ExecuteSingleDataLoad(instruction);
                return;
            }

            // 0000_1100_0001_0000_0000_0000_0000_0000 == 0000_0100_0000_0000_0000_0000_0000_0000
            if ((instruction & 0xc100000) == 0x4000000) //bit 20 not set is store
            {
                // STR
                decoded = "STR";
                ArmSingleDataTransfer++;
                ExecuteSingleDataStore(instruction);
                return;
            }

            if ((instruction & 0x0F000000) == 0x0F000000) //bits 27-8 == 0b1111
            {
                decoded = "SWI";
                ArmSwi++;
                ExecuteSoftwareInterrupt(instruction);
                return;
            }

            if ((instruction & 0x0FFFFFF0) == 0x012FFF10) //bits 27-8 == 0001_0010_1111_1111_1111
            {
                // BX
                decoded = "BX";
                ExecuteArmBranchExchange(instruction);
                ArmBranchExchange++;
                return;
            }

            //equivalent mask
            //((instruction & 0x0FB00FF0) == 0x01000090)
            if (((instruction >> 23) & 0x1F) == 0x2 && //bits 27-23 == 0b00010
                ((instruction >> 20) & 0x3) == 0x0 && //bits 21-20 == 0b00
                ((instruction >> 4) & 0xFF) == 0x9) //bits 11-4 == 0000_1001
            {
                // SWP, SWPB
                decoded = "SWP/SWPB";
                ExecuteArmSingleDataSwap(instruction);
                ArmSingleDataSwap++;
                return;
            }

            if ((instruction & 0x0FC000F0) == 0x00000090)
            {
                decoded = "MULTIPLY";
                this.ExecuteArmMultiply(instruction);
                ArmMultiply++;
                return;
            }

            if ((instruction & 0x0F8000F0) == 0x00800090)
            {
                decoded = "MULTIPLY LONG";
                this.ExecuteArmMultiplyLong(instruction);
                ArmMultiplyLong++;
                return;
            }

            // 0000_1110_0001_0000_0000_0000_1001_0000 == 0000_0000_0001_0000_0000_0000_1001_0000
            if ((instruction & 0x0E100090) == 0x100090)
            {
                // LDRH, LDRSB, LDRSH
                decoded = "LDRH, LDRSB, LDRSH";
                ExecuteHalfwordSignedDataLoad(instruction);
                ArmHalfwordSignedDataTransfer++;
                return;
            }

            // 0000_1110_0001_0000_0000_0000_1001_0000 == 0000_0000_0000_0000_0000_0000_1001_0000
            if ((instruction & 0x0E100090) == 0x90)
            {
                // STRH
                decoded = "STRH";
                ExecuteHalfwordDataStore(instruction);
                ArmHalfwordSignedDataTransfer++;
                return;
            }

            if ((instruction & 0x0FBF0FFF) == 0x010F0000)
            {
                //MRS
                decoded = "MRS";
                ExecuteMrs(instruction);
                ArmMrs++;
                return;
            }

            //MSR
            if ((instruction & 0x0DB0F000) == 0x0120F000)
            {
                decoded = "MSR";
                ExecuteMsr(instruction);
                ArmMsr++;
                return;
            }

            decoded = "DATA PROC";
            ExecuteArmDataProcessing(instruction);
            ArmDataProc++;
        }
        finally
        {
            //var trace = new CpuTrace(instructionAddress, instruction, Registers.Cpsr.ThumbState, Registers.Cpsr.Mode, Registers[0],
            //    Registers[1], Registers[2], Registers[3], Registers[12], Registers.StackPointer, Registers.LinkRegister,
            //    pcBeforeExecute, Registers.ProgramCounter, Registers.Cpsr.ToUInt32(), decoded);
            //DebugUtilities.AddTrace(_traces, trace, ref _traceIndex);
        }
    }

    private void StepThumb()
    {
        var instructionAddress = Registers.ProgramCounter;
        var instruction = bus.Read16(instructionAddress);
        Registers.ProgramCounter = instructionAddress + 2;

        var pcBeforeExecute = Registers.ProgramCounter;
        var decoded = "NONE";
        try
        {
            if ((instruction & 0xE000) == 0) //bits 15-13 == 0
            {
                if ((instruction >> 11) == 0b11)
                {
                    //Format 2
                    decoded = "ADD/SUB f2";
                    this.ExecuteThumbFormat2(instruction);
                    ThumbFormat2++;
                    return;
                }
                //format 1
                decoded = "LSL/LSR/ASR f1";
                this.ExecuteThumbFormat1(instruction);
                ThumbFormat1++;
                return;
            }

            if ((instruction & 0xE000) == 0x2000) //bits 15-13 == 0b001
            {
                //format 3
                decoded = "MOV/CMP/ADD/SUB f3";
                this.ExecuteThumbFormat3(instruction);
                ThumbFormat3++;
                return;
            }

            if ((instruction & 0xF800) == 0x4000) //bits 15-11 == 0b01000
            {
                if (((instruction >> 10) & 1) == 0)
                {
                    //format 4
                    decoded = "ALU OP f4";
                    this.ExecuteThumbFormat4(instruction);
                    ThumbFormat4++;
                    return;
                }
                //format 5
                decoded = "ADD/CMP/MOV/bx f5";
                ThumbFormat5++;
                this.ExecuteThumbFormat5(instruction);
                return;
            }

            if ((instruction & 0xF800) == 0x4800) //bits 15-11 == 0b01001
            {
                //format 6
                decoded = "LDR PC f6";
                this.ExecuteThumbFormat6(instruction);
                ThumbFormat6++;
                return;
            }

            if ((instruction & 0xF000) == 0x5000) //bits 15-12 == 0b0101
            {
                if (((instruction >> 9) & 1) == 0)
                {
                    //format 7
                    decoded = "LDR/STR f7";
                    this.ExecuteThumbFormat7(instruction);
                    ThumbFormat7++;
                    return;
                }
                //format 8
                decoded = "LDR/STR seHW f8";
                this.ExecuteThumbFormat8(instruction);
                ThumbFormat8++;
                return;
            }

            if ((instruction & 0xE000) == 0x6000) //bits 15-13 == 0b011
            {
                //format 9
                decoded = "LDR/STR immOff f9";
                this.ExecuteThumbFormat9(instruction);
                ThumbFormat9++;
                return;
            }

            if ((instruction & 0xF000) == 0x8000) //bits 15-12 == 0b1000
            {
                //format 10
                decoded = "LDR/STR HW f10";
                this.ExecuteThumbFormat10(instruction);
                ThumbFormat10++;
                return;
            }

            if ((instruction & 0xF000) == 0x9000) //bits 15-12 == 0b1001
            {
                //format 11
                decoded = "LDR/STR SP rel f11";
                this.ExecuteThumbFormat11(instruction);
                ThumbFormat11++;
                return;
            }

            if ((instruction & 0xF000) == 0xA000) //bits 15-12 == 0b1010
            {
                //format 12
                decoded = "SP or PC Load f12";
                this.ExecuteThumbFormat12(instruction);
                ThumbFormat12++;
                return;
            }

            if ((instruction & 0xFF00) == 0xB000) //bits 15-8 == 0b10110000
            {
                //format 13
                decoded = "offset SP f13";
                this.ExecuteThumbFormat13(instruction);
                ThumbFormat13++;
                return;
            }

            if ((instruction & 0xF600) == 0xB400) //bits 15-12 == 0b1011 and bits 10-9 == 0b10
            {
                //format 14
                decoded = "PUSH/POP reg f14";
                ThumbFormat14++;
                this.ExecuteThumbFormat14(instruction);
                return;
            }

            if ((instruction & 0xF000) == 0xC000) //bits 15-12 == 0b1100
            {
                //format 15
                decoded = "mult Load/store f15";
                ThumbFormat15++;
                this.ExecuteThumbFormat15(instruction);
                return;
            }

            if ((instruction & 0xFF00) == 0xDF00) //bits 15-8 == 0b11011111
            {
                //format 17
                decoded = "SWI f17";
                ThumbFormat17++;
                this.ExecuteThumbFormat17(instruction);
                return;
            }

            if ((instruction & 0xF000) == 0xD000) //bits 15-12 == 0b1101
            {
                //format 16
                decoded = "COND B f16";
                this.ExecuteThumbFormat16(instruction);
                ThumbFormat16++;
                return;
            }

            if ((instruction & 0xF800) == 0xE000) //bits 15-11 == 0b11100
            {
                //format 18
                decoded = "B f18";
                this.ExecuteThumbFormat18(instruction);
                ThumbFormat18++;
                return;
            }

            if ((instruction & 0xF000) == 0xF000) //bits 15-12 == 0b1111
            {
                //format 19
                decoded = "Long BL f19";
                this.ExecuteThumbFormat19(instruction);
                ThumbFormat19++;
                return;
            }

            decoded = "NOTHING";
            throw new NotSupportedException($"THUMB instruction could not be decoded instruction: {instruction:x4}");
        }
        finally
        {
            //var trace = new CpuTrace(instructionAddress, instruction, Registers.Cpsr.ThumbState, Registers.Cpsr.Mode, Registers[0],
            //    Registers[1], Registers[2], Registers[3], Registers[12], Registers.StackPointer, Registers.LinkRegister,
            //    pcBeforeExecute, Registers.ProgramCounter, Registers.Cpsr.ToUInt32(), decoded);
            //DebugUtilities.AddTrace(_traces, trace, ref _traceIndex);
        }
    }

    private uint DecodeImmediateOperand(uint instruction, out bool carryOut)
    {
        var immediate = instruction & 0xFF;
        var rotate = (int)((instruction >> 8) & 0xF) * 2;
        var result = BitUtils.RotateRight(immediate, rotate);
        carryOut = rotate == 0 ? Registers.Cpsr.Carry : BitUtils.IsBitSet(result, 31);
        return result;
    }

    private uint ComputeShiftedRegisterOperand(uint instruction, out bool carryOut)
    {
        var rm = (int)(instruction & 0xF);
        var rs = (int)(instruction >> 8) & 0xF;
        var registerShift = BitUtils.IsBitSet(instruction, 4);
        var shiftAmount = registerShift
            ? (int)(Registers[rs] & 0xFF)
            : (int)((instruction >> 7) & 0x1F);

        var shiftType = (instruction >> 5) & 0x3;
        var value = rm == 15
            ? registerShift
                ? Registers.ProgramCounter + 8 // rn and/or rm = instAddr + 12 if shifted register operand
                : Registers.ProgramCounter + 4 //otherwise instAddr + 8
            : Registers[rm];

        return shiftType switch
        {
            0 => ShiftLeft(value, shiftAmount, out carryOut),
            1 => ShiftRightLogical(value, shiftAmount, registerShift, out carryOut),
            2 => ShiftRightArithmetic(value, shiftAmount, registerShift, out carryOut),
            3 => RotateRight(value, shiftAmount, registerShift, out carryOut),
            _ => throw new UnreachableException()
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ConditionPassed(Condition condition) =>
        condition switch
        {
            Condition.Eq => Registers.Cpsr.Zero,
            Condition.Ne => !Registers.Cpsr.Zero,
            Condition.Cs => Registers.Cpsr.Carry,
            Condition.Cc => !Registers.Cpsr.Carry,
            Condition.Mi => Registers.Cpsr.Negative,
            Condition.Pl => !Registers.Cpsr.Negative,
            Condition.Vs => Registers.Cpsr.Overflow,
            Condition.Vc => !Registers.Cpsr.Overflow,
            Condition.Hi => Registers.Cpsr is { Carry: true, Zero: false },
            Condition.Ls => !Registers.Cpsr.Carry || Registers.Cpsr.Zero,
            Condition.Ge => Registers.Cpsr.Negative == Registers.Cpsr.Overflow,
            Condition.Lt => Registers.Cpsr.Negative != Registers.Cpsr.Overflow,
            Condition.Gt => !Registers.Cpsr.Zero && Registers.Cpsr.Negative == Registers.Cpsr.Overflow,
            Condition.Le => Registers.Cpsr.Zero || Registers.Cpsr.Negative != Registers.Cpsr.Overflow,
            Condition.Al => true, _ => false
        };

    private void EnterIrqException()
    {
        var nextInstructionAddress = Registers.ProgramCounter;
        Registers.ProgramCounter = 0x18;
        Registers.SetSpsr(CpuMode.Irq, Registers.Cpsr);
        Registers.Cpsr = new ProgramStatusRegister
        {
            Mode = CpuMode.Irq,
            IrqDisable = true,
            ThumbState = false,
            Negative = Registers.Cpsr.Negative,
            Zero = Registers.Cpsr.Zero,
            Carry = Registers.Cpsr.Carry,
            Overflow = Registers.Cpsr.Overflow
        };
        //TODO reg mode
        Registers[14] = nextInstructionAddress + 4u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateNz(uint result)
    {
        Registers.Cpsr.Negative = (result & 0x80000000) != 0;
        Registers.Cpsr.Zero = result == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetCarry(bool carry) =>
        Registers.Cpsr.Carry = carry;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetOverflow(bool overflow) =>
        Registers.Cpsr.Overflow = overflow;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetNegative(bool negative) =>
        Registers.Cpsr.Negative = negative;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetZero(bool zero) =>
        Registers.Cpsr.Zero = zero;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateArithmeticFlags(uint left, uint right, uint result, bool subtraction)
    {
        Registers.Cpsr.Negative = (result & 0x80000000) != 0;
        Registers.Cpsr.Zero = result == 0;

        if (subtraction)
        {
            Registers.Cpsr.Carry = left >= right;
            Registers.Cpsr.Overflow = ((left ^ right) & (left ^ result) & 0x80000000) != 0;
        }
        else
        {
            Registers.Cpsr.Carry = result < left || result < right;
            Registers.Cpsr.Overflow = (~(left ^ right) & (left ^ result) & 0x80000000) != 0;
        }
    }

    private uint ShiftLeft(uint value, int amount, out bool carryOut)
    {
        switch (amount)
        {
            case 0:
                carryOut = Registers.Cpsr.Carry;
                return value;
            case >= 32:
                //last bit shifted out if == 32 otherwise always no carry out
                carryOut = amount == 32 && BitUtils.IsBitSet(value, 0);
                return 0;
            default:
                carryOut = ((value >> (32 - amount)) & 1U) != 0;
                return value << amount;
        }
    }

    private uint ShiftRightLogical(uint value, int amount, bool registerShift, out bool carryOut)
    {
        switch (amount)
        {
            case 0 when registerShift:
                carryOut = Registers.Cpsr.Carry;
                return value;
            case 0:
                carryOut = BitUtils.IsBitSet(value, 31);
                return 0;
            case >= 32:
                carryOut = amount == 32 && BitUtils.IsBitSet(value, 31);
                return 0;
            default:
                carryOut = ((value >> (amount - 1)) & 1U) != 0;
                return value >> amount;
        }
    }

    private uint ShiftRightArithmetic(uint value, int amount, bool registerShift, out bool carryOut)
    {
        switch (amount)
        {
            case 0 when !registerShift:
            case >= 32:
                carryOut = BitUtils.IsBitSet(value, 31);
                return carryOut ? 0xFFFFFFFF : 0;
            case 0:
                carryOut = Registers.Cpsr.Carry;
                return value;
            default:
                carryOut = ((value >> (amount - 1)) & 1U) != 0;
                return (uint)((int)value >> amount);
        }
    }

    private uint RotateRight(uint value, int amount, bool registerShift, out bool carryOut)
    {
        if (amount == 0 && !registerShift) //ror#0 is interpreted as rrx#1, like ror#1 but result bit 31 is set to old C
        {
            carryOut = BitUtils.IsBitSet(value, 0);
            var rotated = BitUtils.RotateRight(value, 1);
            rotated = BitUtils.SetBit(rotated, 31, Registers.Cpsr.Carry);
            return rotated;
        }

        var result = BitUtils.RotateRight(value, amount);
        carryOut = amount == 0 ?
            Registers.Cpsr.Carry : //when rs == 0 carry remains unchanged
            BitUtils.IsBitSet(result, 31);
        return result;
    }
}