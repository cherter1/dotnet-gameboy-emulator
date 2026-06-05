using GbaEmulator.Core.Common;

namespace GbaEmulator.Core.Cpu.Debugger;

public sealed class ThumbInstructionDecoder
{
    private static string DecodeFormat1(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rs = (instruction >> 3) & 0b111;
        string ins = ((instruction >> 11) & 0b11) switch
        {
            0b00 => "LSL",
            0b01 => "LSR",
            0b10 => "ASR",
            _ => "INVALID"
        };

        return $"{ins} r{rd}, {rs}";
    }

    private static string DecodeFormat2(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rs = (instruction >> 3) & 0b111;
        var immediate = BitUtils.IsBitSet(instruction, 10);
        var isSub = BitUtils.IsBitSet(instruction, 9);
        var rnImm = (instruction >> 6) & 0b111;
        string ins = isSub ? "SUB" : "ADD";
        var symbol = immediate ? "#" : "r";

        return $"{ins} r{rd}, r{rs}, {symbol}{rnImm}";
    }

    private static string DecodeFormat3(ushort instruction)
    {
        var rd = (instruction >> 8) & 0b111;
        var offset = (uint)(instruction & 0xFF);

        string ins = ((instruction >> 11) & 0b11) switch
        {
            0b00 => "MOV",
            0b01 => "CMP",
            0b10 => "ADD",
            0b11 => "SUB",
            _ => "INVALID"
        };
        return $"{ins} r{rd}, #{offset}";
    }

    private static string DecodeFormat4(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rs = (instruction >> 3) & 0b111;

        string ins = ((instruction >> 6) & 0xF) switch
        {
            0b0000 => "AND",
            0b0001 => "EOR",
            0b0010 => "LSL",
            0b0011 => "LSR",
            0b0100 => "ASR",
            0b0101 => "ADC",
            0b0110 => "SBC",
            0b0111 => "ROR",
            0b1000 => "TST",
            0b1001 => "NEG",
            0b1010 => "CMP",
            0b1011 => "CMN",
            0b1100 => "ORR",
            0b1101 => "MUL",
            0b1110 => "BIC",
            0b1111 => "MVN",
            _ => "INVALID"
        };

        return $"{ins} r{rd}, r{rs}";
    }

    private static string DecodeFormat5(ushort instruction)
    {
        var rd = ((instruction >> 4) & 0x8) | (instruction & 0b111);
        var rs = (instruction >> 3) & 0xF;

        string ins = ((instruction >> 6) & 0xF) switch
        {
            0b00 => "ADD",
            0b01 => "CMP",
            0b10 => "MOV",
            0b11 => "BX",
            _ => "INVALID"
        };

        return ins == "BX"
            ? $"BX r{rs}"
            : $"{ins} r{rd}, r{rs}";
    }

    private static string DecodeFormat6(ushort instruction)
    {
        var rd = (instruction >> 8) & 0b111;
        var offset = (instruction & 0xFF) << 2;
        //var pc = (Registers.ProgramCounter + 2) & ~3u;
        return $"LDR r{rd}, [pc, #{offset}]";
    }

    private static string DecodeFormat7(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var ro = (instruction >> 6) & 0b111;
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var byteTransfer = BitUtils.IsBitSet(instruction, 10);
        var ins = isLoad ? "LDR" : "STR";
        var mod = byteTransfer ? "B" : string.Empty;

        return $"{ins}{mod} r{rd}, [r{rb}, r{ro}]";
    }

    private static string DecodeFormat8(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var ro = (instruction >> 6) & 0b111;
        var ins = ((instruction >> 10) & 0b11) switch
        {
            0b00 => "STRH",
            0b01 => "LDRH",
            0b10 => "LDSB",
            0b11 => "LDSH",
            _ => "INVALID"
        };
        return $"{ins} r{rd}, [r{rb}, r{ro}]";
    }

    private static string DecodeFormat9(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var offset = ((instruction >> 6) & 0x1F) << 2;
        var ins = ((instruction >> 11) & 0b11) switch
        {
            0b00 => "STR",
            0b01 => "LDR",
            0b10 => "STRB",
            0b11 => "LDRB",
            _ => "INVALID"
        };

        return $"{ins} r{rd}, [r{rb}, #{offset}]";
    }

    private static string DecodeFormat10(ushort instruction)
    {
        var rd = instruction & 0b111;
        var rb = (instruction >> 3) & 0b111;
        var offset = ((instruction >> 6) & 0x1F) << 1;
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var ins = isLoad ? "LDRH" : "STRH";
        return $"{ins} r{rd}, [r{rb}, #{offset}]";
    }

    private static string DecodeFormat11(ushort instruction)
    {
        var rd = (instruction >> 8) & 0b111;
        var offset = (instruction & 0xFF) << 2;
        var isLoad = BitUtils.IsBitSet(instruction, 11);
        var ins = isLoad ? "LDR" : "STR";
        return $"{ins}, r{rd}, [sp, #{offset}]";
    }

    private static string DecodeFormat12(ushort instruction)
    {
        var source = BitUtils.IsBitSet(instruction, 11);
        var immediate = (instruction & 0xFF) << 2;
        var rd = (instruction >> 8) & 0b111;
        var rs = source ? "sp" : "pc";

        return $"ADD r{rd}, {rs}, #{immediate}";
    }

    private static string DecodeFormat13(ushort instruction)
    {
        return $"";
    }
}