using System.Diagnostics;
using System.Numerics;
using GbaEmulator.Core.Common;
using GbaEmulator.Core.Interrupts;
using GbaEmulator.Core.Memory;

namespace GbaEmulator.Core.Cpu;

//Armv4TM arm version
public sealed partial class Arm7Tdmi(GbaBus bus, InterruptController interrupts)
{
    private readonly CpuTrace?[] _traces = new CpuTrace?[4096];
    private int _traceIndex;
    public RegisterBank Registers { get; private set; } = null!;
    public ProgramStatusRegister Cpsr { get; private set; } = new() { Mode = CpuMode.System, IrqDisable = true };

    public void Reset(bool skipBios)
    {
        Registers = new RegisterBank(() => Cpsr.Mode);
        Registers.InitializeForGba();

        Cpsr = new ProgramStatusRegister
        {
            Mode = skipBios ? CpuMode.System : CpuMode.Supervisor,
            IrqDisable = true,
            ThumbState = false
        };

        Registers.ProgramCounter = skipBios ? 0x08000000u : 0u;
    }

    public void SetThumbState(bool enabled) =>
        Cpsr = ProgramStatusRegister.FromUInt32(BitUtils.SetBit(Cpsr.ToUInt32(), 5, enabled));

    public int Step()
    {
        try
        {
            if (interrupts.ShouldServiceIrq(Cpsr.IrqDisable))
            {
                EnterIrqException();
                return 4;
            }

            if (Registers.ProgramCounter % 2 == 1)
            {
                DebugUtilities.DumpTrace(_traces, ref _traceIndex);
            }

            if (Registers.ProgramCounter == 0x0 && Cpsr.Mode == CpuMode.System)
            {
                Console.WriteLine("Bios in system mode");
                var y = 1;
            }

            if (Registers.ProgramCounter == 0x08025254) //snprintf
            {
                var z = 1;
            }

            return Cpsr.ThumbState ? StepThumb() : StepArm();
        }
        catch (Exception)
        {
            DebugUtilities.DumpTrace(_traces, ref _traceIndex);
            throw;
        }
    }

    private int StepArm()
    {
        //TODO: figure out cycles to return
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
                    throw new Exception();
                }
                return 1;
            }

            var bits27_25 = (instruction >> 25) & 0b111;

            if (bits27_25 == 0b101)
            {
                // B, BL
                decoded = BitUtils.IsBitSet(instruction, 24) ? "BL" : "B";
                ExecuteArmBranch(instruction);
                return 3;
            }

            if (bits27_25 == 0b100)
            {
                // LDM, STM
                decoded = "LDM/STM";
                return ExecuteBlockDataTransfer(instruction);
            }

            var bits27_26 = (instruction >> 26) & 0b11;
            if (bits27_26 == 0b01)
            {
                // LDR, STR
                decoded = "LDR/STR";
                ExecuteArmSingleDataTransfer(instruction);
                return 3;
            }

            if ((instruction & 0x0F000000) == 0x0F000000) //bits 27-8 == 0b1111
            {
                //TODO: SWI
                decoded = "SWI";
                ExecuteSoftwareInterrupt(instruction);
                return 4;
            }

            if (bits27_26 == 0b00)
            {
                if ((instruction & 0x0FFFFFF0) == 0x012FFF10) //bits 27-8 == 0001_0010_1111_1111_1111
                {
                    // BX
                    decoded = "BX";
                    ExecuteArmBranchExchange(instruction);
                    return 3;
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
                    return 3;
                }

                if ((instruction & 0x0FC000F0) == 0x00000090)
                {
                    //TODO: Multiply
                    decoded = "MULTIPLY";
                    return 1;
                }

                if ((instruction & 0x0F8000F0) == 0x00800090)
                {
                    decoded = "MULTIPLY LONG";
                    //TODO: MultiplyLong
                    return 1;
                }

                if ((instruction & 0x0E000090) == 0x00000090)
                {
                    // LDRH, STRH, LDRSB, LDRSH
                    decoded = "LDRH, STRH, LDRSB, LDRSH";
                    ExecuteHalfwordSignedDataTransfer(instruction);
                    return 3;
                }

                if ((instruction & 0x010F0FFF) == 0x010F0000)
                {
                    //MRS
                    decoded = "MRS";
                    ExecuteMrs(instruction);
                    return 1;
                }

                //MSR
                if ((instruction & 0x0DB0F000) == 0x0120F000)
                {
                    decoded = "MSR";
                    ExecuteMsr(instruction);
                    return 1;
                }

                decoded = "DATA PROC";
                ExecuteArmDataProcessing(instruction);
                return 1;
            }

            if ((instruction & 0x0C000000) == 0x04000000)
            {
                decoded = "NO IDEA WHAT THIS MASK IS";
                return 3;
            }

            decoded = "NOT SUPPORTED";
            throw new NotSupportedException($"Unhandled ARM instruction 0x{instruction:X8}");

        }
        finally
        {
            var trace = new CpuTrace(instructionAddress, instruction, Cpsr.ThumbState, Cpsr.Mode, Registers[0],
                Registers[1], Registers[2], Registers[3], Registers[12], Registers.StackPointer, Registers.LinkRegister,
                pcBeforeExecute, Registers.ProgramCounter, Cpsr.ToUInt32(), decoded);
            DebugUtilities.AddTrace(_traces, trace, ref _traceIndex);
        }

    }

    private int StepThumb()
    {
        /*
           |..........1 ..................0|
           |5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
        1  |0_0_0|OP_|_Offset5_|_Rs__|_Rd__| LSL, LSR, ASR (lo reg 5 bit shifter imm value)
        3  |0_0_1|OP_|_Rd__|__Offset8______| MOV, CMP, ADD, SUB (8b imm)
        4  |0_1_0_0_0_0|__OP___|_Rs__|_Rd__| ALU OPS (Lo reg pair) - AND, EOR, LSL, LSR, ASR, ADC, SBC, ROR, TST, NEG, CMP, CMN, ORR, MUL, BIC, MVN
        5  |0_1_0_0_0_1|OP_|H|H|Rs/Hs|Rd/Hd| (h1-7, h2-6) - ADD, CMP, MOV (lo and hi reg or hi reg pair), BX
        6  |0_1_0_0_1|_Rd__|____Word8______| PC-Relative load (LDR with PC)
        7  |0_1_0_1|L|B|0|_Ro__|_Rb__|_Rd__| Load/Store with reg offset
        8  |0_1_0_1|H|S|1|_Ro__|_Rb__|_Rd__| Load/Store sign-extended byte/halfword
        9  |0_1_1|B|L|_Offset5_|_Rb__|_Rd__| Load/Store with immediate offset
       10  |1_0_0_0|L|_Offset5_|_Rb__|_Rd__| Load/Store halfword
       11  |1_0_0_1|L|_Rd__|____Word8______| SP-relative Load/Store
       12  |1_0_1_0|S|_Rd__|____Word8______| (S = pc or sp) - Load address
       13  |1_0_1_1_0_0_0_0|S|__SWord8_____| (S = sign flag) - add offset to stack pointer
       14  |1_0_1_1|L|1_0|R|____RList______| push/pop registers
       15  |1_1_0_0|L|_Rb__|____RList______| multiple load/store
       16  |1_1_0_1|_Cond__|____SOffset8___| conditional branch
       17  |1_1_0_1_1_1_1_1|____Value8_____| software interrupt
       18  |1_1_1_0_0|_____Offset11________| unconditional branch
       19  |1_1_1_1|H|_____Offset11________| long branch with link
           no BLX for this cpu only since its armv4T
         */
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
                }
                else
                {
                    //format 1
                    decoded = "LSL/LSR/ASR f1";
                    this.ExecuteThumbFormat1(instruction);
                }
                return 1;
            }

            if ((instruction & 0xE000) == 0x2000) //bits 15-13 == 0b001
            {
                //format 3
                decoded = "MOV/CMP/ADD/SUB f3";
                this.ExecuteThumbFormat3(instruction);
                return 1;
            }

            if ((instruction & 0xF800) == 0x4000) //bits 15-11 == 0b01000
            {
                if (((instruction >> 10) & 1) == 0)
                {
                    //format 4
                    decoded = "ALU OP f4";
                    this.ExecuteThumbFormat4(instruction);
                    return 1;
                }
                else
                {
                    //format 5
                    decoded = "ADD/CMP/MOV/bx f5";
                    return this.ExecuteThumbFormat5(instruction);
                }
            }

            if ((instruction & 0xF800) == 0x4800) //bits 15-11 == 0b01001
            {
                //format 6
                decoded = "LDR PC f6";
                this.ExecuteThumbFormat6(instruction);
                return 2;
            }

            if ((instruction & 0xF000) == 0x5000) //bits 15-12 == 0b0101
            {
                if (((instruction >> 9) & 1) == 0)
                {
                    //format 7
                    decoded = "LDR/STR f7";
                    this.ExecuteThumbFormat7(instruction);
                }
                else
                {
                    //format 8
                    decoded = "LDR/STR seHW f8";
                    this.ExecuteThumbFormat8(instruction);
                }

                return 2;
            }

            if ((instruction & 0xE000) == 0x6000) //bits 15-13 == 0b011
            {
                //format 9
                decoded = "LDR/STR immOff f9";
                this.ExecuteThumbFormat9(instruction);
                return 2;
            }

            if ((instruction & 0xF000) == 0x8000) //bits 15-12 == 0b1000
            {
                //format 10
                decoded = "LDR/STR HW f10";
                this.ExecuteThumbFormat10(instruction);
                return 2;
            }

            if ((instruction & 0xF000) == 0x9000) //bits 15-12 == 0b1001
            {
                //format 11
                decoded = "LDR/STR SP rel f11";
                this.ExecuteThumbFormat11(instruction);
                return 2;
            }

            if ((instruction & 0xF000) == 0xA000) //bits 15-12 == 0b1010
            {
                //format 12
                decoded = "SP or PC Load f12";
                this.ExecuteThumbFormat12(instruction);
                return 2;
            }

            if ((instruction & 0xFF00) == 0xB000) //bits 15-8 == 0b10110000
            {
                //format 13
                decoded = "offset SP f13";
                this.ExecuteThumbFormat13(instruction);
                return 1;
            }

            if ((instruction & 0xF600) == 0xB400) //bits 15-12 == 0b1011 and bits 10-9 == 0b10
            {
                //format 14
                decoded = "PUSH/POP reg f14";
                return this.ExecuteThumbFormat14(instruction);
            }

            if ((instruction & 0xF000) == 0xC000) //bits 15-12 == 0b1100
            {
                //format 15
                decoded = "mult Load/store f15";
                return this.ExecuteThumbFormat15(instruction);
            }

            if ((instruction & 0xFF00) == 0xDF00) //bits 15-8 == 0b11011111
            {
                //format 17
                decoded = "SWI f17";
                this.ExecuteThumbFormat17(instruction);
                return 4;
            }

            if ((instruction & 0xF000) == 0xD000) //bits 15-12 == 0b1101
            {
                //format 16
                decoded = "COND B f16";
                this.ExecuteThumbFormat16(instruction);
                return 3;
            }

            if ((instruction & 0xF800) == 0xE000) //bits 15-11 == 0b11100
            {
                //format 18
                decoded = "B f18";
                this.ExecuteThumbFormat18(instruction);
                return 3;
            }

            if ((instruction & 0xF000) == 0xF000) //bits 15-12 == 0b1111
            {
                //format 19
                decoded = "Long BL f19";
                this.ExecuteThumbFormat19(instruction);
                return 3;
            }

            decoded = "NOTHING";
            throw new NotSupportedException("THUMB instruction could not be decoded");
        }
        finally
        {
            var trace = new CpuTrace(instructionAddress, instruction, Cpsr.ThumbState, Cpsr.Mode, Registers[0],
                Registers[1], Registers[2], Registers[3], Registers[12], Registers.StackPointer, Registers.LinkRegister,
                pcBeforeExecute, Registers.ProgramCounter, Cpsr.ToUInt32(), decoded);
            DebugUtilities.AddTrace(_traces, trace, ref _traceIndex);
        }
    }

    private void ExecuteArmBranch(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|1_0_1|L|___________________Offset______________________| B,BL,BLX
         */
        if (instruction == 0xea000059)
        {
            var x = 1;
        }

        var link = BitUtils.IsBitSet(instruction, 24);
        var offset = BitUtils.SignExtend((int)(instruction & 0x00FFFFFF) << 2, 26);
        var pc = Registers.ProgramCounter;
        if (link)
        {
            Registers[14] = pc;
        }

        //docs say +8 but only do +4 because StepArm() function also adds 4
        Registers.ProgramCounter = (uint)(pc + 4 + offset);
    }

    private void ExecuteArmBranchExchange(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|0_0_0_1_0_0_1_0_1_1_1_1_1_1_1_1_1_1_1_1|0_0|L|1|__Rn___| BX,BLX
           no BLX for this cpu only since its armv4T
         */

        if (instruction == 0xe12fff11)
        {
            var x = 0;
        }
        var rn = (int)instruction & 0xF;
        var target = Registers[rn];

        var cpsr = Cpsr.ToUInt32();
        cpsr = BitUtils.SetBit(cpsr, 5, (target & 1) != 0); //Set Thumb State
        Cpsr = ProgramStatusRegister.FromUInt32(cpsr);

        target &= ~1u; //clear bit 0 because to realign memory
        Registers.ProgramCounter = target;

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

        Registers.SetSpsr(CpuMode.Supervisor, Cpsr);
        Registers[14] = Registers.ProgramCounter;

        var newCpsr = Cpsr.ToUInt32();
        newCpsr = (newCpsr & ~0x1Fu) | (uint)CpuMode.Supervisor;
        newCpsr = BitUtils.SetBit(newCpsr, 7, true);
        newCpsr = BitUtils.SetBit(newCpsr, 5, false);

        Cpsr = ProgramStatusRegister.FromUInt32(newCpsr);
        Registers.ProgramCounter = 0x8; //vector address 0x8
    }

    private int ExecuteBlockDataTransfer(uint instruction)
    {
        /*
           |..3 ..................2 ..................1 ..................0|
           |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
           |_Cond__|1_0_0|P|U|S|W|L|__Rn___|_________Register_List_________| LDM, STM
         */

        var isPreIndex = BitUtils.IsBitSet(instruction, 24);
        var isUp = BitUtils.IsBitSet(instruction, 23);
        //TODO: Later add modes
        //var forcePsrOrUser = BitUtils.IsBitSet(instruction, 22);
        var isWriteback = BitUtils.IsBitSet(instruction, 21);
        var isLoad = BitUtils.IsBitSet(instruction, 20);
        var rn = (int)(instruction >> 16) & 0x0F;

        var registerList = (ushort)(instruction & 0xFFFF);
        if (registerList == 0)
        {
            throw new NotSupportedException("Empty LDM/STM Register list");
        }

        int count = BitOperations.PopCount(registerList);
        uint bytes = (uint)(count * 4);

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

        uint address = startAddress;

        for (int tReg = 0; tReg < 16; tReg++)
        {
            var shouldTransfer = BitUtils.IsBitSet(instruction, tReg);
            if (!shouldTransfer)
                continue;

            if (isLoad)
            {
                uint value = bus.Read32(address);
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
                uint value = tReg == 15
                    ? Registers.ProgramCounter + 4
                    : Registers[tReg];
                bus.Write32(address, value);
            }

            address += 4;
        }

        if (isWriteback)
        {
            Registers[rn] = finalAddress;
        }

        return count + 1;
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
        }
        else
        {
            var temp = bus.Read32(address);
            bus.Write32(address, Registers[rm]);
            Registers[rd] = temp;
        }
    }

    private void ExecuteArmSingleDataTransfer(uint instruction)
    {
        /*
          |..3 ..................2 ..................1 ..................0|
          |1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0_9_8_7_6_5_4_3_2_1_0|
          |_Cond__|0_1_0|P|U|B|W|L|__Rn___|__Rd___|_________Offset________| TransImm9
          |_Cond__|0_1_1|P|U|B|W|L|__Rn___|__Rd___|__Shift__|Typ|0|__Rm___| TransReg9
         */
        if (instruction == 0xe510f004)
        {
            var x = 1;
        }

        var isOffsetImmediate = (instruction & 0x02000000) == 0;
        var preIndex = BitUtils.IsBitSet(instruction, 24);
        var addOffset = BitUtils.IsBitSet(instruction, 23);
        var byteTransfer = BitUtils.IsBitSet(instruction, 22);
        var load = BitUtils.IsBitSet(instruction, 20);
        var baseRegister = (int)((instruction >> 16) & 0xF);
        var destinationRegister = (int)((instruction >> 12) & 0xF);
        var offset = isOffsetImmediate
            ? instruction & 0xFFF
            : ComputeShiftedRegisterOperand(instruction, out _);

        var address = baseRegister == 15
            ? Registers[baseRegister] + 4
            : Registers[baseRegister];
        var effectiveAddress = preIndex
            ? addOffset ? address + offset : address - offset
            : address;

        if (load)
        {
            Registers[destinationRegister] = byteTransfer
                ? bus.Read8(effectiveAddress)
                : bus.Read32(effectiveAddress);
        }
        else if (byteTransfer)
        {
            var writeValue = destinationRegister == 15
                ? Registers[destinationRegister] + 8
                : Registers[destinationRegister];
            bus.Write8(effectiveAddress, (byte)writeValue);
        }
        else
        {
            var writeValue = destinationRegister == 15
                ? Registers[destinationRegister] + 8
                : Registers[destinationRegister];
            bus.Write32(effectiveAddress, writeValue);
        }

        if (!preIndex)
        {
            Registers[baseRegister] = addOffset ? address + offset : address - offset;
        }
    }

    private void ExecuteHalfwordSignedDataTransfer(uint instruction)
    {
        //TODO: happen
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
        var isLoad = BitUtils.IsBitSet(instruction, 20);
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

        if (isLoad)
        {
            switch (opCode)
            {
                case 0b00: //reserved for swp
                    throw new NotSupportedException("opcode 0b00 should be reserved for a SWP instruction");
                case 0b01: //unsigned halfword
                    Registers[rd] = bus.Read16(effectiveAddress);
                    break;
                case 0b10: //signed byte
                    Registers[rd] = (uint)(sbyte)bus.Read8(effectiveAddress);
                    break;
                case 0b11: //signed halfword
                    var rawHalfword = bus.Read16(effectiveAddress);
                    Registers[rd] = (uint)BitUtils.SignExtend(rawHalfword, 16);
                    break;
                default:
                    throw new NotSupportedException("not a possible opcode for this singed/halfword data transfer");
            }

            if (rd == 15)
            {
                Registers.ProgramCounter = Registers[rd] & ~3u;
            }
        }
        else //store
        {
            if (opCode != 0b01)
            {
                throw new NotSupportedException("Only STRH is supported for halfword/signed store");
            }

            uint value = rd == 15
                ? Registers.ProgramCounter + 4
                : Registers[rd];
            bus.Write16(effectiveAddress, (ushort)value);
        }

        if (isPreIndex && isWriteback || !isPreIndex)
        {
            Registers[rn] = updatedAddress;
        }
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
        var statusReg = pSource ? Registers.GetSpsr(Cpsr.Mode).ToUInt32() : Cpsr.ToUInt32();

        Registers[rd] = statusReg;
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
            ? Registers.GetSpsr(Cpsr.Mode).ToUInt32()
            : Cpsr.ToUInt32();

        uint newPsr = flagBits switch
        {
            true when controlBits => source,
            true => (oldPsr & 0x0FFFFFFFu) | (source & 0xF0000000u),
            _ => (oldPsr & 0xFFFFFF00u) | (source & 0xFFu)
        };

        var status = ProgramStatusRegister.FromUInt32(newPsr);
        if (useSpsr)
        {
            Registers.SetSpsr(Cpsr.Mode, status);
        }
        else
        {
            Cpsr = status;
        }
    }

    private uint DecodeImmediateOperand(uint instruction, out bool carryOut)
    {
        var immediate = instruction & 0xFF;
        var rotate = (int)((instruction >> 8) & 0xF) * 2;
        var result = BitUtils.RotateRight(immediate, rotate);
        carryOut = rotate == 0 ? Cpsr.Carry : BitUtils.IsBitSet(result, 31);
        return result;
    }

    private uint ComputeShiftedRegisterOperand(uint instruction, out bool carryOut)
    {
        var rm = (int)(instruction & 0xF);
        var shiftAmount = (int)((instruction >> 7) & 0x1F);
        var shiftType = (instruction >> 5) & 0x3;
        var value = rm == 15
            ? Registers.ProgramCounter + 4
            : Registers[rm];

        if (shiftAmount == 0)
        {
            carryOut = Cpsr.Carry;
            return value;
        }

        return shiftType switch
        {
            0 => ShiftLeft(value, shiftAmount, out carryOut),
            1 => ShiftRightLogical(value, shiftAmount, out carryOut),
            2 => ShiftRightArithmetic(value, shiftAmount, out carryOut),
            3 => RotateRight(value, shiftAmount, out carryOut),
            _ => throw new UnreachableException()
        };
    }

    private bool ConditionPassed(Condition condition) =>
        condition switch
        {
            Condition.Eq => Cpsr.Zero,
            Condition.Ne => !Cpsr.Zero,
            Condition.Cs => Cpsr.Carry,
            Condition.Cc => !Cpsr.Carry,
            Condition.Mi => Cpsr.Negative,
            Condition.Pl => !Cpsr.Negative,
            Condition.Vs => Cpsr.Overflow,
            Condition.Vc => !Cpsr.Overflow,
            Condition.Hi => Cpsr is { Carry: true, Zero: false },
            Condition.Ls => !Cpsr.Carry || Cpsr.Zero,
            Condition.Ge => Cpsr.Negative == Cpsr.Overflow,
            Condition.Lt => Cpsr.Negative != Cpsr.Overflow,
            Condition.Gt => !Cpsr.Zero && Cpsr.Negative == Cpsr.Overflow,
            Condition.Le => Cpsr.Zero || Cpsr.Negative != Cpsr.Overflow,
            Condition.Al => true, _ => false
        };

    private void EnterIrqException()
    {
        var nextInstructionAddress = Registers.ProgramCounter;
        Registers.ProgramCounter = 0x18;
        Registers.SetSpsr(CpuMode.Irq, Cpsr);
        Cpsr = new ProgramStatusRegister
        {
            Mode = CpuMode.Irq,
            IrqDisable = true,
            ThumbState = false,
            Negative = Cpsr.Negative,
            Zero = Cpsr.Zero,
            Carry = Cpsr.Carry,
            Overflow = Cpsr.Overflow
        };
        Registers[14] = nextInstructionAddress + 4u;
    }

    private void UpdateNz(uint result)
    {
        var cpsr = Cpsr.ToUInt32();
        // N
        cpsr = BitUtils.SetBit(cpsr, 31, (result & 0x80000000) != 0);
        // Z
        cpsr = BitUtils.SetBit(cpsr, 30, result == 0);
        Cpsr = ProgramStatusRegister.FromUInt32(cpsr);
    }

    public void SetCarry(bool carry) =>
        Cpsr = ProgramStatusRegister.FromUInt32(BitUtils.SetBit(Cpsr.ToUInt32(), 29, carry));

    public void SetOverflow(bool overflow) =>
        Cpsr = ProgramStatusRegister.FromUInt32(BitUtils.SetBit(Cpsr.ToUInt32(), 28, overflow));

    public void SetNegative(bool negative) =>
        Cpsr = ProgramStatusRegister.FromUInt32(BitUtils.SetBit(Cpsr.ToUInt32(), 31, negative));

    public void SetZero(bool zero) =>
        Cpsr = ProgramStatusRegister.FromUInt32(BitUtils.SetBit(Cpsr.ToUInt32(), 30, zero));

    private void UpdateArithmeticFlags(uint left, uint right, uint result, bool subtraction)
    {
        var cpsr = Cpsr.ToUInt32();
        // N
        cpsr = BitUtils.SetBit(cpsr, 31, (result & 0x80000000) != 0);
        // Z
        cpsr = BitUtils.SetBit(cpsr, 30, result == 0);

        if (subtraction)
        {
            // C
            cpsr = BitUtils.SetBit(cpsr, 29, left >= right);
            // V
            cpsr = BitUtils.SetBit(cpsr, 28, ((left ^ right) & (left ^ result) & 0x80000000) != 0);
        }
        else
        {
            // C
            cpsr = BitUtils.SetBit(cpsr, 29, result < left || result < right);
            // V
            cpsr = BitUtils.SetBit(cpsr, 28, (~(left ^ right) & (left ^ result) & 0x80000000) != 0);
        }

        Cpsr = ProgramStatusRegister.FromUInt32(cpsr);
    }

    private static uint ShiftLeft(uint value, int amount, out bool carryOut)
    {
        carryOut = ((value >> (32 - amount)) & 1U) != 0;
        return value << amount;
    }

    private static uint ShiftRightLogical(uint value, int amount, out bool carryOut)
    {
        carryOut = ((value >> (amount - 1)) & 1U) != 0;
        return value >> amount;
    }

    private static uint ShiftRightArithmetic(uint value, int amount, out bool carryOut)
    {
        carryOut = ((value >> (amount - 1)) & 1U) != 0;
        return (uint)((int)value >> amount);
    }

    private static uint RotateRight(uint value, int amount, out bool carryOut)
    {
        var result = BitUtils.RotateRight(value, amount);
        carryOut = BitUtils.IsBitSet(result, 31);
        return result;
    }
}