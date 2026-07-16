using GbaEmulator.Core.Common;

namespace GbaEmulator.Core.Cpu;

public sealed partial class Arm7Tdmi
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

        var result = Registers[rm] * Registers[rs];
        if (accumulate)
        {
            result += Registers[rn];
        }

        if (setFlags)
        {
            SetNegative(BitUtils.IsBitSet(result, 31));
            SetZero(result == 0);
        }

        Registers[rd] = result;
    }

    private void ExecuteArmMultiplyLong(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_0_0_0_1|U|A|S|__RdHi_|__RdLo_|__Rs___|1_0_0_1|__Rm___| Mul
         */

        var rm = instruction & 0xF;
        var rs = (instruction >> 8) & 0xf;
        var rdLo = (instruction >> 12) & 0xf;
        var rdHi = (instruction >> 16) & 0xf;
        var setFlags = BitUtils.IsBitSet(instruction, 20);
        var accumulate = BitUtils.IsBitSet(instruction, 21);
        var signed = BitUtils.IsBitSet(instruction, 22);
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
        if (rd == 15)
        {
            var x = 1;
        }

        var operand1 = rn == 15 
            ? BitUtils.IsBitSet(instruction, 4) && !immediate
                ? Registers.ProgramCounter + 8 // rn and/or rm = instAddr + 12 if shifted register operand
                : Registers.ProgramCounter + 4 //otherwise instAddr + 8
            : Registers[rn];

        var operand2 = immediate
            ? DecodeImmediateOperand(instruction, out var carryOut)
            : ComputeShiftedRegisterOperand(instruction, out carryOut);

        var cy = Cpsr.Carry ? 1u : 0u;
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
                    SetCarry(carryOut);
                }

                break;
            case 0x1: //EOR
                result = operand1 ^ operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    SetCarry(carryOut);
                }

                break;
            case 0x2: //SUB
                result = operand1 - operand2;
                Registers[rd] = result;
                if (setFlags && rd == 15)
                {
                    var restoredPsr = Registers.GetSpsr(Cpsr.Mode);
                    Cpsr = restoredPsr;
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
                    SetCarry(wide >> 32 != 0);
                }

                break;
            case 0x6: //SBC
                wide = (ulong)operand1 - operand2 + cy - 1u;
                Registers[rd] = (uint)wide;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand1, operand2, (uint)wide, subtraction: true);
                    //Set Carry after to set it correctly
                    SetCarry((long)wide >= 0);
                }

                break;
            case 0x7: //RSC
                wide = (ulong)operand2 - operand1 + cy - 1u;
                Registers[rd] = (uint)wide;
                if (setFlags)
                {
                    UpdateArithmeticFlags(operand2, operand1, (uint)wide, subtraction: true);
                    //Set Carry after to set it correctly
                    SetCarry((long)wide >= 0);
                }

                break;
            case 0x08: //TST
                result = operand1 & operand2;
                UpdateNz(result);
                SetCarry(carryOut);

                break;
            case 0x09: //TEQ
                result = operand1 ^ operand2;
                UpdateNz(result);
                SetCarry(carryOut);

                break;
            case 0xA: //CMP
                result = operand1 - operand2;
                UpdateArithmeticFlags(operand1, operand2, result, subtraction: true);

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
                    SetCarry(carryOut);
                }

                break;
            case 0xD: //MOV
                result = operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    SetCarry(carryOut);
                }

                if (rd == 15 && setFlags)
                {
                    var oldMode = Cpsr.Mode;
                    Cpsr = Registers.GetSpsr(oldMode);
                }

                break;
            case 0xE: //BIC
                result = operand1 & ~operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    SetCarry(carryOut);
                }

                break;
            case 0xF: //MVN
                result = ~operand2;
                Registers[rd] = result;
                if (setFlags)
                {
                    UpdateNz(result);
                    SetCarry(carryOut);
                }

                break;
            default:
                throw new NotSupportedException($"ARM opcode 0x{opcode:X} is not implemented yet.");
        }
    }
}