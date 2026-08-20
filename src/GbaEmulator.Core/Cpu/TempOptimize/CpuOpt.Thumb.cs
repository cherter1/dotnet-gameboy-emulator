using System.Numerics;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Cpu;

public sealed partial class CpuOpt
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
                    Registers.Cpsr.Carry = BitUtils.IsBitSet((uint)sourceValue, 32 - offset);
                }

                break;
            case 0b01: //LSR
                if (offset == 0)
                {
                    result = 0;
                    Registers.Cpsr.Carry = BitUtils.IsBitSet((uint)sourceValue, 31);
                    break;
                }

                result = (uint)(sourceValue >>> offset);
                Registers.Cpsr.Carry = BitUtils.IsBitSet((uint)sourceValue, offset - 1);

                break;
            case 0b10: //ASR
                if (offset == 0)
                {
                    var carryOut = BitUtils.IsBitSet((uint)sourceValue, 31);
                    result = carryOut ? 0xFFFFFFFF : 0;
                    Registers.Cpsr.Carry = carryOut;
                    break;
                }

                result = (uint)(sourceValue >> offset);
                Registers.Cpsr.Carry = BitUtils.IsBitSet((uint)sourceValue, offset - 1);

                break;
            default:
                throw new NotSupportedException("not a valid opCode for Thumb format 1");
        }

        Registers[rd] = result;
        UpdateNz(result);

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
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

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
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

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
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

        var cy = Registers.Cpsr.Carry ? 1u : 0u;
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
                Registers.Cpsr.Carry = carryOut;
                UpdateNz(result);
                Registers[rd] = result;

                _cycles++; //I
                break;
            case 0b0011: //LSR
                shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.ShiftRightLogical(Registers[rd], shiftAmount, true, out carryOut);
                Registers.Cpsr.Carry = carryOut;
                UpdateNz(result);
                Registers[rd] = result;

                _cycles++; //I
                break;
            case 0b0100: //ASR
                shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.ShiftRightArithmetic(Registers[rd], shiftAmount, true, out carryOut);
                Registers.Cpsr.Carry = carryOut;
                UpdateNz(result);
                Registers[rd] = result;

                _cycles++; //I
                break;
            case 0b0101: //ADC
                var wide = (ulong)Registers[rd] + Registers[rs] + cy;
                result = (uint)wide;
                UpdateArithmeticFlags(Registers[rd], Registers[rs], result, subtraction: false);
                Registers.Cpsr.Carry = wide >> 32 != 0;
                Registers[rd] = result;

                break;
            case 0b0110: //SBC
                var longResult = (ulong)Registers[rd] - Registers[rs] + cy - 1u;
                result = (uint)longResult;
                UpdateArithmeticFlags(Registers[rd], Registers[rs], result, subtraction: true);
                Registers.Cpsr.Carry = (long)longResult >= 0;
                Registers[rd] = result;

                break;
            case 0b0111: //ROR
                shiftAmount = (int)(Registers[rs] & 0xFF);
                result = this.RotateRight(Registers[rd], shiftAmount, true, out carryOut);
                UpdateNz(result);
                if (shiftAmount != 0)
                {
                    Registers.Cpsr.Carry = carryOut;
                }
                Registers[rd] = result;

                _cycles++; //I
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
                var multiplierOperand = Registers[rd];
                result = multiplierOperand * Registers[rs];
                Registers[rd] = result;
                UpdateNz(result);
                Registers.Cpsr.Carry = false;

                _cycles += GetMultiplierArrayCycles(multiplierOperand, false); //mI cycles
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

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
    }

    private void ExecuteThumbFormat5(ushort instruction)
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
        var destOperand = rd == 15 ? Registers[rd] + 2 : Registers[rd];

        switch (opCode)
        {
            case 0b00: //ADD
                Registers[rd] = destOperand + source;
                if (rd == 15)
                {
                    Registers[rd] &= ~1u;

                    //refill pipeline adds 1S and 1N
                    _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: false);
                    _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
                }

                break;
            case 0b01: //CMP
                var result = destOperand - source;
                UpdateArithmeticFlags(destOperand, source, result, subtraction: true);

                break;
            case 0b10: //MOV
                Registers[rd] = source;
                if (rd == 15)
                {
                    Registers[rd] &= ~1u;

                    //refill pipeline adds 1S and 1N
                    _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: false);
                    _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
                }

                break;
            case 0b11: //BX
                var target = source;
                var setThumb = (target & 1) != 0;

                Registers.Cpsr.ThumbState = (target & 1) != 0;

                //32 bit align if entering arm else 16 bit aligned
                target &= setThumb ? ~1u : ~3u;
                Registers.ProgramCounter = target;

                _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter,
                    setThumb ? AccessWidth.Halfword : AccessWidth.Word, sequential: false); //N
                _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter,
                    setThumb ? AccessWidth.Halfword : AccessWidth.Word, sequential: true) * 2; //2S
                return;
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
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

        _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word, sequential: false); //N
        _cycles++; //I
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true); //S
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
            if (byteTransfer)
            {
                Registers[rd] = bus.Read8(effectiveAddress);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //N
            }
            else
            {
                Registers[rd] = bus.Read32(effectiveAddress);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N
            }

            _cycles++; //I
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true); //S
        }
        else //STR
        {
            if (byteTransfer)
            {
                bus.Write8(effectiveAddress, (byte)Registers[rd]);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //N
            }
            else
            {
                bus.Write32(effectiveAddress, Registers[rd]);
                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N
            }

            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: false); //N
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

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false);
                break;
            case 0b01: //LDSB
                var loadedByte = bus.Read8(effectiveAddress);
                Registers[rd] = (uint)BitUtils.SignExtend(loadedByte, 8);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //N
                _cycles++; //I
                break;
            case 0b10: //LDRH
                uint value = bus.Read16(effectiveAddress);
                if ((effectiveAddress & 1) != 0)
                {
                    value = BitOperations.RotateRight(value, 8);
                }

                Registers[rd] = value;

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false); //N
                _cycles++; //I
                break;
            case 0b11: //LDSH
                var loadedHalfword = bus.Read16(effectiveAddress);
                if ((effectiveAddress & 1) != 0)
                {
                    Registers[rd] = (uint)BitUtils.SignExtend((loadedHalfword >> 8) & 0xff, 8);
                }
                else
                {
                    Registers[rd] = (uint)BitUtils.SignExtend(loadedHalfword, 16);
                }

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false); //N
                _cycles++; //I
                break;
            default:
                throw new NotSupportedException("not a valid opCode for Thumb format 8");
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: opCode != 0); //N for store else S
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

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N
                break;
            case 0b01: //LDR
                Registers[rd] = bus.Read32(effectiveAddress);

                _cycles++; //I
                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N
                break;
            case 0b10: //STRB
                bus.Write8(effectiveAddress, (byte)Registers[rd]);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //N
                break;
            case 0b11: //LDRB
                Registers[rd] = bus.Read8(effectiveAddress);

                _cycles++; //I
                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //N
                break;
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: opCode is 0b01 or 0b11); //N for store else S
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
            uint value = bus.Read16(effectiveAddress);
            if ((effectiveAddress & 1) != 0)
            {
                value = BitOperations.RotateRight(value, 8);
            }

            Registers[rd] = value;

            _cycles++; //I
        }
        else //STRH
        {
            bus.Write16(effectiveAddress, (ushort)Registers[rd]);
        }

        _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false); //N
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: isLoad); //N for store else S
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

            _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N
            _cycles++; //I
        }
        else //STR
        {
            bus.Write32(effectiveAddress, Registers[rd]);

            _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: isLoad); //N for STR else S
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

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true); //S
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

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true); //S
    }

    private void ExecuteThumbFormat14(ushort instruction)
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |1_0_1_1|L|1_0|R|____RList______| push/pop registers
         */

        var isPush = !BitUtils.IsBitSet(instruction, 11);
        if (isPush)
        {
            for (int reg = 8; reg >= 0; reg--)
            {
                var shouldTransfer = BitUtils.IsBitSet(instruction, reg);
                if (!shouldTransfer)
                    continue;

                Registers[13] -= 4;
                var register = Registers[reg];
                bus.Write32(Registers.StackPointer, reg == 8 ? Registers.LinkRegister : register);
                _cycles += bus.GetCpuAccessCycles(Registers.StackPointer, AccessWidth.Halfword, sequential: BitOperations.TrailingZeroCount(instruction) != reg);
            }
        }
        else
        {
            for (int reg = 0; reg <= 8; reg++)
            {
                var shouldTransfer = BitUtils.IsBitSet(instruction, reg);
                if (!shouldTransfer)
                    continue;

                var result = bus.Read32(Registers.StackPointer & ~3u);

                _cycles += bus.GetCpuAccessCycles(Registers.StackPointer, AccessWidth.Halfword,
                    sequential: BitOperations.TrailingZeroCount(instruction) != reg);

                if (reg == 8)
                {
                    Registers[15] = result & ~1u;

                    //refill pipeline
                    _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: false); //N
                    _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true); //S
                }
                else
                {
                    Registers[reg] = result;
                }
                Registers[13] += 4;
            }
            _cycles++; //I
        }
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: !isPush);
    }

    private void ExecuteThumbFormat15(ushort instruction)
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

        if (transferCount == 0)
        {
            Registers[rb] += 0x40;
            if (isLoad)
            {
                Registers[15] = bus.Read32(address & ~3u);
            }
            else
            {
                bus.Write32(address, Registers[15] + 4);
            }

            _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word, sequential: false);
            return;
        }

        for (int reg = 0; reg < 8; reg++)
        {
            var shouldTransfer = BitUtils.IsBitSet(instruction, reg);
            if (!shouldTransfer)
                continue;

            if (isLoad) //LDMIA
            {
                Registers[reg] = bus.Read32(address);
                _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word, sequential: reg != BitOperations.TrailingZeroCount(instruction));
            }
            else //STMIA
            {
                if (reg == rb && reg != BitOperations.TrailingZeroCount(instruction))
                {
                    bus.Write32(address, finalAddress);
                    _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word, sequential: reg != BitOperations.TrailingZeroCount(instruction));
                }
                else
                {
                    bus.Write32(address, Registers[reg]);
                    _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word, sequential: reg != BitOperations.TrailingZeroCount(instruction));
                }
            }

            address += 4u;
        }

        _cycles += isLoad ? 1 : 0; //I cycle for load only

        if (!isLoad || !BitUtils.IsBitSet(instruction, rb))
        {
            Registers[rb] = finalAddress;
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: isLoad);
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
            //1S cycle for unmet conditions
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true);
            return;
        }

        var offset = BitUtils.SignExtend((instruction & 0xFF) << 1, 9);

        Registers.ProgramCounter = (uint)(Registers.ProgramCounter + 2 + offset);

        //refill pipeline
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: false);
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true) * 2; //2S
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

        Registers.SetSpsr(CpuMode.Supervisor, Registers.Cpsr);

        //todo mode
        Registers.Cpsr.Mode = CpuMode.Supervisor;
        Registers.Cpsr.IrqDisable = true;
        Registers.Cpsr.ThumbState = false;

        Registers[14] = Registers.ProgramCounter;
        Registers.ProgramCounter = 0x8; //vector address 0x8

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //N
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true) * 2; //2S cycles
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

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: false);
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Halfword, sequential: true) * 2;
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

        _cycles += 3; //TODO: figure cycles later
    }
}
