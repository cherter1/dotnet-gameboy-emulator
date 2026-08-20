using GbaEmulator.Core.Common;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Cpu;

public sealed partial class CpuOpt
{
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
