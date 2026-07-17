using System.Numerics;
using GbaEmulator.Core.Common;

namespace GbaEmulator.Core.Cpu;

public sealed partial class Arm7Tdmi
{
    private void ExecuteThumbFormat1(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_0_0|OP_|_Offset5_|_Rs__|_Rd__| LSL, LSR, ASR (lo reg 5 bit shifter imm value)
         */

        var rd = instruction & 0b111;
        var rs = (instruction >> 3) & 0b111;
        var offset = (instruction >> 6) & 0x1F;
        var opCode = (instruction >> 11) & 0b11;

        var sourceValue = (int)Registers[rs];

        uint result;
        switch (opCode)
        {
            case 0b00: //LSL
                result = (uint)(sourceValue << offset);
                if (offset != 0)
                {
                    SetCarry(BitUtils.IsBitSet((uint)sourceValue, 32 - offset));
                }

                break;
            case 0b01: //LSR
                if (offset == 0)
                {
                    result = 0;
                    SetCarry(BitUtils.IsBitSet((uint)sourceValue, 31));
                    break;
                }

                result = (uint)(sourceValue >>> offset);
                SetCarry(BitUtils.IsBitSet((uint)sourceValue, offset - 1));

                break;
            case 0b10: //ASR
                if (offset == 0)
                {
                    var carryOut = BitUtils.IsBitSet((uint)sourceValue, 31); 
                    result = carryOut ? 0xFFFFFFFF : 0;
                    SetCarry(carryOut);
                    break;
                }

                result = (uint)(sourceValue >> offset);
                SetCarry(BitUtils.IsBitSet((uint)sourceValue, offset - 1));

                break;
            default:
                throw new NotSupportedException("not a valid opCode for Thumb format 1");
        }

        Registers[rd] = result;

        UpdateNz(result);
    }

    private void ExecuteThumbFormat2(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_0_0|1_1|I|O|_Rni_|_Rs__|_Rd__| ADD, SUB (lo reg or 3 bit imm value)
         */

        var rd = instruction & 0b111;
        var rs = (instruction >> 3) & 0b111;
        var immediate = BitUtils.IsBitSet(instruction, 10);
        var opCode = BitUtils.IsBitSet(instruction, 9);
        var rnImm = (instruction >> 6) & 0b111;
        var operand2 = immediate ? (uint)rnImm : Registers[rnImm];

        uint result;
        if (opCode) //SUB
        {
            result = Registers[rs] - operand2;
        }
        else //ADD
        {
            result = Registers[rs] + operand2;
        }

        UpdateArithmeticFlags(Registers[rs], operand2, result, opCode);
        Registers[rd] = result;
    }

    private void ExecuteThumbFormat3(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_0_1|OP_|_Rd__|__Offset8______| MOV, CMP, ADD, SUB (8b imm)
         */

        var opCode = (instruction >> 11) & 0b11;
        var rd = (instruction >> 8) & 0b111;
        var offset = (uint)(instruction & 0xFF);

        uint result;
        switch (opCode)
        {
            case 0b00: //MOV
                Registers[rd] = offset;
                UpdateNz(offset);

                break;
            case 0b01: //CMP
                result = Registers[rd] - offset;
                UpdateArithmeticFlags(Registers[rd], offset, result, subtraction: true);

                break;
            case 0b10: //ADD
                result = Registers[rd] + offset;
                UpdateArithmeticFlags(Registers[rd], offset, result, subtraction: false);
                Registers[rd] = result;

                break;
            case 0b11: //SUB
                result = Registers[rd] - offset;
                UpdateArithmeticFlags(Registers[rd], offset, result, subtraction: true);
                Registers[rd] = result;

                break;
            default:
                throw new NotSupportedException("not a valid opCode for Thumb format 3");
        }
    }

    private void ExecuteThumbFormat4(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_1_0_0_0_0|__OP___|_Rs__|_Rd__| ALU OPS (Lo reg pair) - AND, EOR, LSL, LSR, ASR, ADC, SBC, ROR, TST, NEG, CMP, CMN, ORR, MUL, BIC, MVN
         */

        var rd = instruction & 0b111;
        var rs = (instruction >> 3) & 0b111;
        var opCode = (instruction >> 6) & 0xF;

        var cy = Cpsr.Carry ? 1u : 0u;
        uint result;
        switch (opCode)
        {
            case 0b0000: //AND
                result = Registers[rd] & Registers[rs];
                UpdateNz(result);
                Registers[rd] = result;

                break;
            case 0b0001: //EOR
                result = Registers[rd] ^ Registers[rs];
                UpdateNz(result);
                Registers[rd] = result;

                break;
            case 0b0010: //LSL
                var shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.ShiftLeft(Registers[rd], shiftAmount, out bool carryOut);
                SetCarry(carryOut);
                UpdateNz(result);
                Registers[rd] = result;

                break;
            case 0b0011: //LSR
                shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.ShiftRightLogical(Registers[rd], shiftAmount, true, out carryOut);
                SetCarry(carryOut);
                UpdateNz(result);
                Registers[rd] = result;

                break;
            case 0b0100: //ASR
                shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.ShiftRightArithmetic(Registers[rd], shiftAmount, true, out carryOut);
                SetCarry(carryOut);
                UpdateNz(result);
                Registers[rd] = result;

                break;
            case 0b0101: //ADC
                var wide = (ulong)Registers[rd] + Registers[rs] + cy;
                result = (uint)wide;
                UpdateArithmeticFlags(Registers[rd], Registers[rs], result, subtraction: false);
                SetCarry(wide >> 32 != 0);
                Registers[rd] = result;

                break;
            case 0b0110: //SBC
                var longResult = (ulong)Registers[rd] - Registers[rs] + cy - 1u;
                result = (uint)longResult;
                UpdateArithmeticFlags(Registers[rd], Registers[rs], result, subtraction: true);
                SetCarry((long)longResult >= 0);
                Registers[rd] = result;

                break;
            case 0b0111: //ROR
                shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.RotateRight(Registers[rd], shiftAmount, true, out carryOut);
                UpdateNz(result);
                SetCarry(carryOut);
                Registers[rd] = result;

                break;
            case 0b1000: //TST
                result = Registers[rd] & Registers[rs];
                UpdateNz(result);

                break;
            case 0b1001: //NEG
                result = 0 - Registers[rs];
                UpdateArithmeticFlags(0, Registers[rs], result, true);
                Registers[rd] = result;

                break;
            case 0b1010: //CMP
                result = Registers[rd] - Registers[rs];
                UpdateArithmeticFlags(Registers[rd], Registers[rs], result, subtraction: true);

                break;
            case 0b1011: //CMN
                result = Registers[rd] + Registers[rs];
                UpdateArithmeticFlags(Registers[rd], Registers[rs], result, subtraction: false);

                break;
            case 0b1100: //ORR
                result = Registers[rd] | Registers[rs];
                Registers[rd] = result;
                UpdateNz(result);

                break;
            case 0b1101: //MUL
                result = Registers[rd] * Registers[rs];
                Registers[rd] = result;
                UpdateNz(result);
                SetCarry(false);

                break;
            case 0b1110: //BIC
                result = Registers[rd] & ~Registers[rs];
                Registers[rd] = result;
                UpdateNz(result);

                break;
            case 0b1111: //MVN
                result = ~Registers[rs];
                Registers[rd] = result;
                UpdateNz(result);

                break;
            default:
                throw new NotSupportedException("not a valid opCode for Thumb format 4");
        }
    }

    private int ExecuteThumbFormat5(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_1_0_0_0_1|OP_|H|H|Rs/Hs|Rd/Hd| (h1-7 high bit for Rd, h2-6 h2 high bit for Rs) - ADD, CMP, MOV (lo and hi reg or hi reg pair), BX
         */

        var opCode = (instruction >> 8) & 0b11;
        var rd = ((instruction >> 4) & 0x8) | (instruction & 0b111);
        var rs = (instruction >> 3) & 0xF;

        var source = rs == 15 ? Registers[rs] + 2 : Registers[rs];

        switch (opCode)
        {
            case 0b00: //ADD
                Registers[rd] += source;
                if (rd == 15)
                {
                    Registers[rd] &= ~1u;
                }

                break;
            case 0b01: //CMP
                var result = Registers[rd] - source;
                UpdateArithmeticFlags(Registers[rd], source, result, subtraction: true);

                break;
            case 0b10: //MOV
                Registers[rd] = source;
                if (rd == 15)
                {
                    Registers[rd] &= ~1u;
                }

                break;
            case 0b11: //BX
                var target = source;
                var setThumb = (target & 1) != 0;
                var cpsr = BitUtils.SetBit(Cpsr.ToUInt32(), 5, setThumb);
                Cpsr = ProgramStatusRegister.FromUInt32(cpsr);

                //32 bit align if entering arm else 16 bit aligned
                target &= setThumb ? ~1u : ~3u;
                Registers.ProgramCounter = target;
                return 3;
        }

        return 1;
    }

    private void ExecuteThumbFormat6(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_1_0_0_1|_Rd__|____Word8______| PC-Relative load (LDR with PC)
         */

        var rd = (instruction >> 8) & 0b111;
        var offset = (instruction & 0xFF) << 2;
        var pc = (Registers.ProgramCounter + 2) & ~3u;
        var address = pc + (uint)offset;
        Registers[rd] = bus.Read32(address);
    }

    private void ExecuteThumbFormat7(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_1_0_1|L|B|0|_Ro__|_Rb__|_Rd__| Load/Store with reg offset
         */

        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var ro = (instruction >> 6) & 0b111;
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var byteTransfer = BitUtils.IsBitSet(instruction, 10);
        var effectiveAddress = Registers[rb] + Registers[ro];

        if (isLoad) //LDR
        {
            Registers[rd] = byteTransfer ? bus.Read8(effectiveAddress) : bus.Read32(effectiveAddress);
        }
        else //STR
        {
            if (byteTransfer)
            {
                bus.Write8(effectiveAddress, (byte)Registers[rd]);
                return;
            }

            bus.Write32(effectiveAddress, Registers[rd]);
        }
    }

    private void ExecuteThumbFormat8(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_1_0_1|H|S|1|_Ro__|_Rb__|_Rd__| Load/Store sign-extended byte/halfword
         */

        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var ro = (instruction >> 6) & 0b111;
        var opCode = (instruction >> 10) & 0b11;

        var effectiveAddress = Registers[rb] + Registers[ro];
        switch (opCode)
        {
            case 0b00: //STRH
                bus.Write16(effectiveAddress, (ushort)Registers[rd]);

                break;
            case 0b01: //LDSB
                var loadedByte = bus.Read8(effectiveAddress);
                Registers[rd] = (uint)BitUtils.SignExtend(loadedByte, 8);

                break;
            case 0b10: //LDRH
                Registers[rd] = bus.Read16(effectiveAddress);

                break;
            case 0b11: //LDSH
                var loadedHalfword = bus.Read16(effectiveAddress);
                Registers[rd] = (uint)BitUtils.SignExtend(loadedHalfword, 16);

                break;
            default:
                throw new NotSupportedException("not a valid opCode for Thumb format 8");
        }
    }

    private void ExecuteThumbFormat9(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |0_1_1|B|L|_Offset5_|_Rb__|_Rd__| Load/Store with immediate offset
         */

        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var opCode = (instruction >> 11) & 0b11;
        var offset = (instruction >> 6) & 0x1F;
        if ((opCode & 0b10) == 0)
        {
            offset <<= 2;
        }
        var effectiveAddress = Registers[rb] + (uint)offset;

        switch (opCode)
        {
            case 0b00: //STR
                bus.Write32(effectiveAddress, Registers[rd]);

                break;
            case 0b01: //LDR
                Registers[rd] = bus.Read32(effectiveAddress);

                break;
            case 0b10: //STRB
                bus.Write8(effectiveAddress, (byte)Registers[rd]);

                break;
            case 0b11: //LDRB
                Registers[rd] = bus.Read8(effectiveAddress);

                break;
        }
    }

    private void ExecuteThumbFormat10(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_0_0_0|L|_Offset5_|_Rb__|_Rd__| Load/Store halfword
         */

        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var offset = ((instruction >> 6) & 0x1F) << 1;
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var effectiveAddress = Registers[rb] + (uint)offset;

        if (isLoad) //LDRH
        {
            Registers[rd] = bus.Read16(effectiveAddress);
        }
        else //STRH
        {
            bus.Write16(effectiveAddress, (ushort)Registers[rd]);
        }
    }

    private void ExecuteThumbFormat11(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_0_0_1|L|_Rd__|____Word8______| SP-relative Load/Store
         */

        var rd = (instruction >> 8) & 0b111;
        var offset = (instruction & 0xFF) << 2;
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var effectiveAddress = Registers.StackPointer + (uint)offset;

        if (isLoad) //LDR
        {
            Registers[rd] = bus.Read32(effectiveAddress);
        }
        else //STR
        {
            bus.Write32(effectiveAddress, Registers[rd]);
        }
    }

    private void ExecuteThumbFormat12(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_0_1_0|S|_Rd__|____Word8______| (S = pc or sp) - Load address
         */

        var source = BitUtils.IsBitSet(instruction, 11);
        var immediate = (instruction & 0xFF) << 2;
        var rd = (instruction >> 8) & 0b111;
        var operand1 = source
            ? Registers.StackPointer
            : (Registers.ProgramCounter + 2) & ~3u;

        Registers[rd] = operand1 + (uint)immediate;
    }

    private void ExecuteThumbFormat13(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_0_1_1_0_0_0_0|S|__SWord7_____| (S = sign flag) - add offset to stack pointer
         */

        var signed = BitUtils.IsBitSet(instruction, 7);
        var imm = (instruction & 0x7F) << 2;

        if (signed)
        {
            Registers[13] = Registers.StackPointer - (uint)imm;
        }
        else
        {
            Registers[13] = Registers.StackPointer + (uint)imm;
        }
    }

    private int ExecuteThumbFormat14(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_0_1_1|L|1_0|R|____RList______| push/pop registers
         */

        var transferCount = 0;
        var isPush = !BitUtils.IsBitSet(instruction, 11);

        if (isPush)
        {
            for (int reg = 8; reg >= 0; reg--)
            {
                var shouldTransfer = BitUtils.IsBitSet(instruction, reg);
                if (!shouldTransfer)
                    continue;

                transferCount++;
                Registers[13] -= 4;
                var register = Registers[reg];
                bus.Write32(Registers.StackPointer, reg == 8 ? Registers.LinkRegister : register);
            }
        }
        else
        {
            for (int reg = 0; reg <= 8; reg++)
            {
                var shouldTransfer = BitUtils.IsBitSet(instruction, reg);
                if (!shouldTransfer)
                    continue;

                transferCount++;
                var result = bus.Read32(Registers.StackPointer);
                if (reg == 8)
                {
                    Registers[15] = result & ~1u;
                }
                else
                {
                    Registers[reg] = result;
                }
                Registers[13] += 4;
            }
        }
        return transferCount + 1;
    }

    private int ExecuteThumbFormat15(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_1_0_0|L|_Rb__|____RList______| multiple load/store
         */

        var transferCount = BitOperations.PopCount(instruction & 0xFFu);
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var rb = (instruction >> 8) & 0b111;
        var address = Registers[rb];
        var finalAddress = address + (uint)(transferCount * 4);

        for (int reg = 0; reg < 8; reg++)
        {
            var shouldTransfer = BitUtils.IsBitSet(instruction, reg);
            if (!shouldTransfer)
                continue;

            if (isLoad) //LDMIA
            {
                Registers[reg] = bus.Read32(address);
            }
            else //STMIA
            {
                if (reg == rb && reg != BitOperations.TrailingZeroCount(instruction))
                {
                    bus.Write32(address, finalAddress);
                }
                else
                {
                    bus.Write32(address, Registers[reg]);
                }
            }

            address += 4u;
        }

        if (!isLoad || !BitUtils.IsBitSet(instruction, rb))
        {
            Registers[rb] = finalAddress;
        }

        return transferCount + 1;
    }

    private void ExecuteThumbFormat16(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_1_0_1|_Cond__|____SOffset8___| conditional branch
         */

        var cond = (instruction >> 8) & 0x0F;
        if (!ConditionPassed((Condition)cond))
        {
            return;
        }

        var offset = BitUtils.SignExtend((instruction & 0xFF) << 1, 9);

        Registers.ProgramCounter = (uint)(Registers.ProgramCounter + 2 + offset);
    }

    private void ExecuteThumbFormat17(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_1_0_1_1_1_1_1|____Value8_____| software interrupt
         */

        var comment = instruction & 0xFF;
        Console.WriteLine("THUMB SWI Enter: comment = " + comment.ToString("X8"));

        Registers.SetSpsr(CpuMode.Supervisor, Cpsr);

        var newCpsr = Cpsr.ToUInt32();
        newCpsr = (newCpsr & ~0x1Fu) | (uint)CpuMode.Supervisor;
        newCpsr = BitUtils.SetBit(newCpsr, 7, true);
        newCpsr = BitUtils.SetBit(newCpsr, 5, false);

        Cpsr = ProgramStatusRegister.FromUInt32(newCpsr);
        Registers[14] = Registers.ProgramCounter;
        Registers.ProgramCounter = 0x8; //vector address 0x8
    }

    private void ExecuteThumbFormat18(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_1_1_0_0|_____Offset11________| unconditional branch
         */

        var offset = BitUtils.SignExtend((instruction & 0x7FF) << 1, 12);
        Registers.ProgramCounter = (Registers.ProgramCounter + 2) + (uint)offset;
    }

    private void ExecuteThumbFormat19(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_1_1_1|H|_____Offset11________| long branch with link
         */

        var h = BitUtils.IsBitSet(instruction, 11);
        var offset = instruction & 0x07FF;
        if (h)
        {
            var temp = Registers.ProgramCounter;
            Registers.ProgramCounter = Registers.LinkRegister + (uint)(offset << 1);
            Registers.ProgramCounter &= ~1u;
            Registers[14] = temp | 1u;
        }
        else
        {
            var signedOffset = BitUtils.SignExtend(offset << 12, 23);
            Registers[14] = (uint)(Registers.ProgramCounter + 2 + signedOffset);
        }
    }
}