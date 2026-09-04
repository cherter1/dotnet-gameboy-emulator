using System.Numerics;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Cpu;

public sealed partial class CpuOpt
{
    private void ExecuteArmBranch(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|1_0_1|L|___________________Offset______________________| B,BL
         */

        var link = BitUtils.IsBitSet(instruction, 24);
        var offset = BitUtils.SignExtend((int)(instruction & 0x00FFFFFF) << 2, 26);
        var pc = Registers.ProgramCounter;
        if (link)
        {
            Registers[14] = pc;
        }

        //docs say +8 but only do +4 because StepArm() function also adds 4
        Registers.ProgramCounter = (uint)(pc + 4 + offset);

        //2S + 1N cycles basically 1S and FlushingPipeline
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //1N
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true) * 2; //2S
    }

    private void ExecuteBlockDataTransfer(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|1_0_0|P|U|S|W|L|__Rn___|_________Register_List_________| LDM, STM
         */

        var isPreIndex = BitUtils.IsBitSet(instruction, 24);
        var isUp = BitUtils.IsBitSet(instruction, 23);
        var forcePsrOrUser = BitUtils.IsBitSet(instruction, 22);
        var isWriteback = BitUtils.IsBitSet(instruction, 21);
        var isLoad = BitUtils.IsBitSet(instruction, 20);
        var rn = (int)(instruction >> 16) & 0x0F;

        var registerList = (ushort)(instruction & 0xFFFF);

        int count = BitOperations.PopCount(registerList);
        uint bytes = (uint)(count * 4);
        bytes = bytes == 0 ? 0x40 : bytes; //if zero transfer count act like full transfer 0x40

        uint baseAddress = rn == 15 ? Registers.ProgramCounter + 4 : Registers[rn];

        uint startAddress, finalAddress;
        if (isUp)
        {
            finalAddress = baseAddress + bytes;
            startAddress = isPreIndex ? baseAddress + 4 : baseAddress;
        }
        else
        {
            finalAddress = baseAddress - bytes;
            startAddress = isPreIndex
                ? baseAddress - bytes
                : baseAddress - bytes + 4;
        }

        if (count == 0)
        {
            Registers[rn] = isUp ? Registers[rn] + 0x40 : Registers[rn] - 0x40;

            if (isLoad)
            {
                Registers[15] = bus.Read32(startAddress & ~3u);
            }
            else
            {
                bus.Write32(startAddress, Registers[15] + 8);
            }

            _cycles += bus.GetCpuAccessCycles(startAddress, AccessWidth.Word, sequential: false);
            return;
        }

        var currentMode = Registers.Cpsr.Mode;
        if (forcePsrOrUser && !BitUtils.IsBitSet(instruction, 15)) // system banked registers if r15 not in list and S=1
        {
            //TODO Mode
            Registers.Cpsr.Mode = CpuMode.System;
        }

        uint address = startAddress;

        for (int tReg = 0; tReg < 16; tReg++)
        {
            var shouldTransfer = BitUtils.IsBitSet(instruction, tReg);
            if (!shouldTransfer)
                continue;

            if (isLoad)
            {
                uint value = bus.Read32(address & ~3u);

                if (tReg == 15)
                {
                    //word align program counter
                    Registers.ProgramCounter = value & ~3u;
                }
                else
                {
                    Registers[tReg] = value;
                }
            }
            else
            {
                if (tReg == rn && tReg != BitOperations.TrailingZeroCount(instruction))
                {
                    bus.Write32(address, finalAddress);
                }
                else
                {
                    uint value = tReg == 15
                        ? Registers.ProgramCounter + 8
                        : Registers[tReg];
                    bus.Write32(address, value);
                }
            }

            _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word,
                sequential: tReg != BitOperations.TrailingZeroCount(instruction)); //First transfer N, the rest are S

            address += 4;
        }

        if (forcePsrOrUser && !BitUtils.IsBitSet(instruction, 15))
        {
            //TODO mode
            Registers.Cpsr.Mode = currentMode;
        }

        if (isWriteback && (!isLoad || !BitUtils.IsBitSet(instruction, rn))) // no writeback for ldm if rn is in Rlist
        {
            Registers[rn] = finalAddress;
        }

        if (isLoad && BitUtils.IsBitSet(instruction, 15))
        {
            //ldm refill pipeline if r15 in rList
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false);
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true);
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: isLoad);
    }

    private void ExecuteSingleDataLoad(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_1_0|P|U|B|W|L|__Rn___|__Rd___|_________Offset________| TransImm9
          |_Cond__|0_1_1|P|U|B|W|L|__Rn___|__Rd___|__Shift__|Typ|0|__Rm___| TransReg9
         */

        var isOffsetImmediate = (instruction & 0x02000000) == 0;
        var preIndex = (instruction & 0x1000000) != 0; //bit 24
        var addOffset = (instruction & 0x800000) != 0; //bit 23
        var byteTransfer = (instruction & 0x400000) != 0; //bit 22
        var writeback = (instruction & 0x200000) != 0; //bit 21

        var baseRegister = (int)((instruction >> 16) & 0xF);
        var destinationRegister = (int)(instruction >> 12) & 0xF;
        var offset = isOffsetImmediate
            ? instruction & 0xFFF
            : ComputeShiftedRegisterOperand(instruction, out _);

        var address = baseRegister == 15
            ? Registers[baseRegister] + 4
            : Registers[baseRegister];
        var effectiveAddress = preIndex
            ? addOffset ? address + offset : address - offset
            : address;

        uint loadedWord;

        if (byteTransfer)
        {
            loadedWord = bus.Read8(effectiveAddress);
            _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //STR N
        }
        else
        {
            loadedWord = bus.Read32(effectiveAddress);
            _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //STR N
        }

        if (!preIndex)
        {
            Registers[baseRegister] = addOffset
                ? address + offset
                : address - offset;
        }
        else if (writeback)
        {
            Registers[baseRegister] = effectiveAddress;
        }

        Registers[destinationRegister] = loadedWord;

        _cycles++; //1I
        if (destinationRegister == 15)
        {
            //if LDR PC add another 1S and 1N for pipeline refill
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false);
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true) * 2;
            return;
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true); //LDR S
    }

    private void ExecuteSingleDataStore(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_1_0|P|U|B|W|L|__Rn___|__Rd___|_________Offset________| TransImm9
          |_Cond__|0_1_1|P|U|B|W|L|__Rn___|__Rd___|__Shift__|Typ|0|__Rm___| TransReg9
         */

        var isOffsetImmediate = (instruction & 0x02000000) == 0;
        var preIndex = (instruction & 0x1000000) != 0; //bit 24
        var addOffset = (instruction & 0x800000) != 0; //bit 23
        var byteTransfer = (instruction & 0x400000) != 0; //bit 22
        var writeback = (instruction & 0x200000) != 0; //bit 21

        var baseRegister = (int)((instruction >> 16) & 0xF);
        var destinationRegister = (int)(instruction >> 12) & 0xF;
        var offset = isOffsetImmediate
            ? instruction & 0xFFF
            : ComputeShiftedRegisterOperand(instruction, out _);

        var address = baseRegister == 15
            ? Registers[baseRegister] + 4
            : Registers[baseRegister];
        var effectiveAddress = preIndex
            ? addOffset ? address + offset : address - offset
            : address;

        var writeValue = destinationRegister == 15
            ? Registers[destinationRegister] + 8
            : Registers[destinationRegister];

        if (byteTransfer)
        {
            bus.Write8(effectiveAddress, (byte)writeValue);

            _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //STR N
        }
        else
        {
            bus.Write32(effectiveAddress, writeValue);

            _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //STR N
        }

        if (!preIndex)
        {
            Registers[baseRegister] = addOffset ? address + offset : address - offset;
        }
        else if (writeback)
        {
            Registers[baseRegister] = effectiveAddress;
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //STR N
    }

    private void ExecuteSoftwareInterrupt(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|1_1_1_1|_____________Ignored_by_Processor______________| SWI
         */

        var comment = instruction & 0x00FFFFFFu;
        Console.WriteLine("ARM SWI: comment = " + comment.ToString("X8"));

        Registers.SetSpsr(CpuMode.Supervisor, Registers.Cpsr);

        var newCpsr = Registers.Cpsr.ToUInt32();
        newCpsr = (newCpsr & ~0x1Fu) | (uint)CpuMode.Supervisor; //set supervisor mode
        newCpsr = BitUtils.SetBit(newCpsr, 7, true); //set irq disable
        newCpsr = BitUtils.SetBit(newCpsr, 5, false); //disable thumb

        //TODO: mode
        Registers.Cpsr = ProgramStatusRegister.FromUInt32(newCpsr);
        Registers[14] = Registers.ProgramCounter;
        Registers.ProgramCounter = 0x8; //vector address 0x8

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //N
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true) * 2; //2S cycles

        var functionVector = comment >> 16;
        if (functionVector == 0x6)
        {
            var q = 1;
        }
        if (false)
        {
            //TODO: temp must add functions in bios
            //div
            var numerator = (int)Registers[0];
            var denominator = (int)Registers[1];
            //TODO: not handling divide by zero
            var result = numerator / denominator;
            Registers[0] = (uint)result;
            var remainder = numerator % denominator;
            Registers[1] = (uint)remainder;
            Registers[3] = (uint)result;
            //var absoluteValue = (uint)result;
        }
    }

    private void ExecuteArmBranchExchange(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|0_0_0_1_0_0_1_0_1_1_1_1_1_1_1_1_1_1_1_1|0_0|L|1|__Rn___| BX
           no BLX for this cpu only since its armv4T
         */

        var rn = (int)instruction & 0xF;
        var target = Registers[rn];

        Registers.Cpsr.ThumbState = (target & 1) != 0;

        target &= ~1u; //clear bit 0 because to realign memory
        Registers.ProgramCounter = target;

        //2S + 1N cycles basically 1S and FlushingPipeline
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //1N
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true); //2S
    }

    private void ExecuteArmSingleDataSwap(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|0_0_0_1_0|B|0_0|__Rn___|__Rd___|0_0_0_0|1_0_0_1|__Rm___| SWP, SWPB
         */

        var byteSwap = BitUtils.IsBitSet(instruction, 22);
        var rd = (int)(instruction >> 12) & 0xF;
        var rn = (int)(instruction >> 16) & 0xF;
        var rm = (int)instruction & 0xF;

        var address = Registers[rn];
        if (byteSwap)
        {
            var temp = bus.Read8(address);
            bus.Write8(address, (byte)(Registers[rm] & 0xFF));
            Registers[rd] = temp;

            _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Byte, sequential: false) * 2; //2N cycles
        }
        else
        {
            var temp = bus.Read32(address);
            bus.Write32(address, Registers[rm]);
            Registers[rd] = temp;

            _cycles += bus.GetCpuAccessCycles(address, AccessWidth.Word, sequential: false) * 2; //2N cycles
        }

        _cycles++; //I
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true); //1S cycle
    }

    private void ExecuteArmMultiply(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0_0_0_0|A|S|__Rd___|__Rn___|__Rs___|1_0_0_1|__Rm___| Mul
         */

        var rm = (int)instruction & 0xF;
        var rs = (int)(instruction >> 8) & 0xf;
        var rn = (int)(instruction >> 12) & 0xf;
        var rd = (int)(instruction >> 16) & 0xf;
        var setFlags = BitUtils.IsBitSet(instruction, 20);
        var accumulate = BitUtils.IsBitSet(instruction, 21);

        var multiplierOperand = Registers[rs];
        var result = Registers[rm] * multiplierOperand;

        int bitMultiplier = 0;
        if (accumulate)
        {
            result += Registers[rn];
            bitMultiplier = 1;
        }

        if (setFlags)
        {
            Registers.Cpsr.Negative = (result & 0x80000000) != 0;
            Registers.Cpsr.Zero = result == 0;
        }

        Registers[rd] = result;

        bitMultiplier += GetMultiplierArrayCycles(multiplierOperand, false);

        // 1S + mI cycles or if accumulate 1S + (m + 1)I cycles
        _cycles += bitMultiplier; //I cycles
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, true); //S cycles
    }

    private static int GetMultiplierArrayCycles(uint multiplierOperand, bool unSigned)
    {
        const uint SingleCycleMask = 0xffffff00;
        const uint DoubleCycleMask = 0xffff0000;
        const uint TripleCycleMask = 0xff000000;
        if (unSigned)
        {
            if ((multiplierOperand & SingleCycleMask) == 0)
            {
                return 1;
            }

            if ((multiplierOperand & DoubleCycleMask) == 0)
            {
                return 2;
            }

            return (multiplierOperand & TripleCycleMask) == 0 ? 3 : 4;
        }

        if ((multiplierOperand & SingleCycleMask) is 0 or SingleCycleMask)
        {
            return 1;
        }

        if ((multiplierOperand & DoubleCycleMask) is 0 or DoubleCycleMask)
        {
            return 2;
        }

        return (multiplierOperand & TripleCycleMask) is 0 or TripleCycleMask ? 3 : 4;
    }

    private void ExecuteArmMultiplyLong(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0_0_1|U|A|S|__RdHi_|__RdLo_|__Rs___|1_0_0_1|__Rm___| Mull
         */

        var rm = (int)(instruction & 0xF);
        var rs = (int)((instruction >> 8) & 0xf);
        var rdLo = (int)((instruction >> 12) & 0xf);
        var rdHi = (int)((instruction >> 16) & 0xf);
        var setFlags = BitUtils.IsBitSet(instruction, 20);
        var accumulate = BitUtils.IsBitSet(instruction, 21);
        var signed = BitUtils.IsBitSet(instruction, 22);
        var multiplierOperand = Registers[rs];

        int bitMultiplier = 1;
        if (signed)
        {
            //signed mul
            long res = (long)(int)Registers[rm] * (int)multiplierOperand;
            if (accumulate)
            {
                long acc = (long)(((ulong)Registers[rdHi] << 32) | Registers[rdLo]);
                res += acc;

                bitMultiplier += 1;
            }

            Registers[rdLo] = (uint)(res & 0xFFFFFFFF);
            Registers[rdHi] = (uint)(res >> 32);

            if (!setFlags)
            {
                return;
            }

            Registers.Cpsr.Negative = ((res >> 32) & 0x80000000) != 0;
            Registers.Cpsr.Zero = res == 0;
            bitMultiplier += GetMultiplierArrayCycles(multiplierOperand, false);
        }
        else
        {
            //unsigned mul
            var res = (ulong)Registers[rm] * multiplierOperand;
            if (accumulate)
            {
                ulong acc = ((ulong)Registers[rdHi] << 32) | Registers[rdLo];
                res += acc;

                bitMultiplier += 1;
            }

            Registers[rdLo] = (uint)(res & 0xFFFFFFFF);
            Registers[rdHi] = (uint)(res >> 32);

            if (!setFlags)
            {
                return;
            }

            Registers.Cpsr.Negative = ((res >> 32) & 0x80000000) != 0;
            Registers.Cpsr.Zero = res == 0;
            bitMultiplier += GetMultiplierArrayCycles(multiplierOperand, true);
        }

        //1S + (m+1)I cycles unless accumulate then 1S + (m+2)I cycles
        _cycles += bitMultiplier; //I cycles
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, true); //S cycles
    }

    private void ExecuteHalfwordSignedDataLoad(uint instruction)
    {
        //LDRH, STRH, LDRSB, LDRSH
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0|P|U|0|W|L|__Rn___|__Rd___|0_0_0_0|1|S|H|1|__Rm___| reg offset
          |_Cond__|0_0_0|P|U|1|W|L|__Rn___|__Rd___|_H_Off_|1|S|H|1|_L_Off_| imm offset
         */

        var rn = (int)(instruction >> 16) & 0xF;
        var baseAddress = rn == 15
            ? Registers.ProgramCounter + 4
            : Registers[rn];
        var rd = (int)(instruction >> 12) & 0xF;
        var opCode = (instruction >> 5) & 0b11;
        var isWriteback = BitUtils.IsBitSet(instruction, 21);
        var immediate = BitUtils.IsBitSet(instruction, 22);
        var isUp = BitUtils.IsBitSet(instruction, 23);
        var isPreIndex = BitUtils.IsBitSet(instruction, 24);

        var immOffset = ((instruction >> 4) & 0xF0) | (instruction & 0x0F);
        var rm = (int)instruction & 0x0F;
        var offset = immediate ? immOffset : Registers[rm];

        var updatedAddress = isUp
            ? baseAddress + offset
            : baseAddress - offset;
        var effectiveAddress = isPreIndex
            ? updatedAddress
            : baseAddress;

        uint loadedValue;
        switch (opCode)
        {
            case 0b00: //reserved for swp
                throw new NotSupportedException("opcode 0b00 should be reserved for a SWP instruction");
            case 0b01: //unsigned halfword
                loadedValue = bus.Read16(effectiveAddress);
                loadedValue = BitOperations.RotateRight(loadedValue, (int)((effectiveAddress & 1u) * 8));

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false); //N cycle
                break;
            case 0b10: //signed byte
                loadedValue = (uint)(sbyte)bus.Read8(effectiveAddress);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Byte, sequential: false); //N cycle
                break;
            case 0b11: //signed halfword
                var rawHalfword = bus.Read16(effectiveAddress);
                if ((effectiveAddress & 1) != 0)
                {
                    loadedValue = (uint)BitUtils.SignExtend((rawHalfword >> 8) & 0xff, 8);
                    _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false); //N cycle
                    break;
                }
                loadedValue = (uint)BitUtils.SignExtend(rawHalfword, 16);

                _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Halfword, sequential: false); //N cycle
                break;
            default:
                throw new NotSupportedException("not a possible opcode for this singed/halfword data transfer");
        }

        if (isPreIndex && isWriteback || !isPreIndex)
        {
            Registers[rn] = updatedAddress;
        }

        Registers[rd] = loadedValue;
        if (rd == 15)
        {
            Registers.ProgramCounter = loadedValue & ~3u;

            //refill pipeline
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //N cycle
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true); //S cycle
        }

        _cycles++; //I cycle
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true); //S cycle
    }

    private void ExecuteHalfwordDataStore(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0|P|U|0|W|L|__Rn___|__Rd___|0_0_0_0|1|S|H|1|__Rm___| reg offset
          |_Cond__|0_0_0|P|U|1|W|L|__Rn___|__Rd___|_H_Off_|1|S|H|1|_L_Off_| imm offset
         */

        var rn = (int)(instruction >> 16) & 0xF;
        var baseAddress = rn == 15
            ? Registers.ProgramCounter + 4
            : Registers[rn];
        var rd = (int)(instruction >> 12) & 0xF;
        var opCode = (instruction >> 5) & 0b11;
        var isWriteback = BitUtils.IsBitSet(instruction, 21);
        var immediate = BitUtils.IsBitSet(instruction, 22);
        var isUp = BitUtils.IsBitSet(instruction, 23);
        var isPreIndex = BitUtils.IsBitSet(instruction, 24);

        var immOffset = ((instruction >> 4) & 0xF0) | (instruction & 0x0F);
        var rm = (int)instruction & 0x0F;
        var offset = immediate ? immOffset : Registers[rm];

        var updatedAddress = isUp
            ? baseAddress + offset
            : baseAddress - offset;
        var effectiveAddress = isPreIndex
            ? updatedAddress
            : baseAddress;

        if (opCode != 0b01)
        {
            throw new NotSupportedException("Only STRH is supported for halfword/signed store");
        }

        uint value = rd == 15
            ? Registers.ProgramCounter + 4
            : Registers[rd];
        bus.Write16(effectiveAddress, (ushort)value);
        _cycles += bus.GetCpuAccessCycles(effectiveAddress, AccessWidth.Word, sequential: false); //N cycle

        if (isPreIndex && isWriteback || !isPreIndex)
        {
            Registers[rn] = updatedAddress;
        }
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false); //STR N
    }

    private void ExecuteMrs(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0_1_0|P|0_0_1_1_1_1|__Rd___|0_0_0_0_0_0_0_0_0_0_0_0| MRS reg
         */

        var pSource = BitUtils.IsBitSet(instruction, 22);
        var rd = (int)(instruction >> 12) & 0xF;
        var statusReg = pSource ? Registers.GetSpsr().ToUInt32() : Registers.Cpsr.ToUInt32();

        Registers[rd] = statusReg;

        //1S cycle
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true);
    }

    private void ExecuteMsr(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0_1_0|P|1_0|F|0_0|C|1_1_1_1|0_0_0_0_0_0_0_0|__Rm___| MSR reg
          |_Cond__|0_0|I|1_0|P|1_0|F|0_0|C|1_1_1_1|_Shift_|___Immediate___| MSR imm
         */

        var useSpsr = BitUtils.IsBitSet(instruction, 22);
        var immediate = BitUtils.IsBitSet(instruction, 25);
        var flagBits = BitUtils.IsBitSet(instruction, 19);
        var controlBits = BitUtils.IsBitSet(instruction, 16);

        var rm = (int)instruction & 0xF;
        var source = immediate
            ? DecodeImmediateOperand(instruction, out _)
            : Registers[rm];

        var oldPsr = useSpsr
            ? Registers.GetSpsr().ToUInt32()
            : Registers.Cpsr.ToUInt32();

        uint newPsr = flagBits switch
        {
            true when controlBits => source,
            true => (oldPsr & 0x0FFFFFFFu) | (source & 0xF0000000u),
            false when controlBits => (oldPsr & 0xFFFFFF00u) | (source & 0xFFu),
            _ => oldPsr
        };

        var status = ProgramStatusRegister.FromUInt32(newPsr);
        if (useSpsr)
        {
            Registers.SetSpsr(Registers.Cpsr.Mode, status);
        }
        else
        {
            //TODO: Mode
            Registers.Cpsr = status;
        }

        //1S cycle
        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true);
    }

    private void ExecuteArmDataProcessing(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0|___Op__|S|__Rn___|__Rd___|__Shift__|Typ|0|__Rm___| DataProc
          |_Cond__|0_0_0|___Op__|S|__Rn___|__Rd___|__Rs___|0|Typ|1|__Rm___| DataProc
          |_Cond__|0_0_1|___Op__|S|__Rn___|__Rd___|_Shift_|___Immediate___| DataProc
         */

        var immediate = BitUtils.IsBitSet(instruction, 25);
        var opcode = (instruction >> 21) & 0xF;
        var setFlags = BitUtils.IsBitSet(instruction, 20);
        var rn = (int)((instruction >> 16) & 0xF);
        var rd = (int)((instruction >> 12) & 0xF);

        var operand1 = rn == 15
            ? BitUtils.IsBitSet(instruction, 4) && !immediate
                ? Registers.ProgramCounter + 8 // rn and/or rm = instAddr + 12 if shifted register operand
                : Registers.ProgramCounter + 4 //otherwise instAddr + 8
            : Registers[rn];

        var operand2 = immediate
            ? DecodeImmediateOperand(instruction, out var logicalCarryOut)
            : ComputeShiftedRegisterOperand(instruction, out logicalCarryOut);

        var cy = Registers.Cpsr.Carry ? 1u : 0u;
        uint result;
        ulong wide;
        switch (opcode)
        {
            case 0x0: //AND
                result = operand1 & operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    Registers.Cpsr.Carry = logicalCarryOut;
                }

                break;
            case 0x1: //EOR
                result = operand1 ^ operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    Registers.Cpsr.Carry = logicalCarryOut;
                }

                break;
            case 0x2: //SUB
                result = operand1 - operand2;
                Registers[rd] = result;
                if (setFlags && rd == 15)
                {
                    var restoredPsr = Registers.GetSpsr();
                    Registers.Cpsr = restoredPsr;
                }
                if (rd != 15 && setFlags)
                {
                    UpdateArithmeticFlags(operand1, operand2, result, subtraction: true);
                }

                break;
            case 0x3: //RSB
                result = operand2 - operand1;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand2, operand1, result, subtraction: true);
                }

                break;
            case 0x4: //ADD
                result = operand1 + operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand1, operand2, result, subtraction: false);
                }

                break;
            case 0x5: //ADC
                wide = (ulong)operand1 + operand2 + cy;
                result = (uint)wide;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand1, operand2, result, subtraction: false);
                    //Set Carry after to set it correctly
                    Registers.Cpsr.Carry = wide >> 32 != 0;
                }

                break;
            case 0x6: //SBC
                wide = (ulong)operand1 - operand2 + cy - 1u;
                Registers[rd] = (uint)wide;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand1, operand2, (uint)wide, subtraction: true);
                    //Set Carry after to set it correctly
                    Registers.Cpsr.Carry = (long)wide >= 0;
                }

                break;
            case 0x7: //RSC
                wide = (ulong)operand2 - operand1 + cy - 1u;
                Registers[rd] = (uint)wide;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand2, operand1, (uint)wide, subtraction: true);
                    //Set Carry after to set it correctly
                    Registers.Cpsr.Carry = (long)wide >= 0;
                }

                break;
            case 0x08: //TST
                result = operand1 & operand2;
                UpdateNz(result);
                Registers.Cpsr.Carry = logicalCarryOut;

                break;
            case 0x09: //TEQ
                result = operand1 ^ operand2;
                UpdateNz(result);
                Registers.Cpsr.Carry = logicalCarryOut;

                break;
            case 0xA: //CMP
                result = operand1 - operand2;
                UpdateArithmeticFlags(operand1, operand2, result, subtraction: true);
                if (rd == 15 && setFlags)
                {
                    var oldMode = Registers.Cpsr.Mode;
                    if (oldMode != CpuMode.User && oldMode != CpuMode.System)
                    {
                        Registers.Cpsr = Registers.GetSpsr();
                    }
                    //Registers.ProgramCounter += 4;
                }

                break;
            case 0xB: //CMN
                result = operand1 + operand2;
                UpdateArithmeticFlags(operand1, operand2, result, subtraction: false);

                break;
            case 0xC: //ORR
                result = operand1 | operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    Registers.Cpsr.Carry = logicalCarryOut;
                }

                break;
            case 0xD: //MOV
                result = operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    Registers.Cpsr.Carry = logicalCarryOut;
                }

                if (rd == 15 && setFlags)
                {
                    Registers.Cpsr = Registers.GetSpsr();
                    //TODO mode
                }

                break;
            case 0xE: //BIC
                result = operand1 & ~operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    Registers.Cpsr.Carry = logicalCarryOut;
                }

                break;
            case 0xF: //MVN
                result = ~operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    Registers.Cpsr.Carry = logicalCarryOut;
                }

                break;
            default:
                throw new NotSupportedException($"ARM opcode 0x{opcode:X} is not implemented yet.");
        }

        if (rd == 15)
        {
            //simulates pipeline flush adding extra cycle S cycle and N cycle
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: false);
            _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true);
        }

        if (!immediate && BitUtils.IsBitSet(instruction, 4))
        {
            //if register shift then add I cycle
            _cycles += 1;
        }

        _cycles += bus.GetCpuAccessCycles(Registers.ProgramCounter, AccessWidth.Word, sequential: true);
    }
}