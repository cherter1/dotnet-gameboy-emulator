using System.Numerics;
using System.Runtime.ExceptionServices;
using GbaEmulator.Core.Bios;
using GbaCartridge = GbaEmulator.Core.Cartridge.Cartridge;

namespace GbaEmulator.Core.Memory;

public sealed class GbaBus(GbaMemory memory)
{
    public void LoadBios(BiosImage? bios)
    {
        if (bios is not null)
        {
            Array.Copy(bios.Bytes, memory.Bios, Math.Min(memory.Bios.Length, bios.Bytes.Length));
        }
    }

    public void LoadCartridge(GbaCartridge? cartridge) => memory.Rom = cartridge?.RomData ?? [];

    public uint Read32(uint address)
    {
        var aligned = address & ~3u;
        var region = ResolveRegion(aligned, out var buffer, out var offset);

        uint raw = region switch
        {
            MemoryRegion.Io => memory.Io.ReadIo32Aligned(aligned),
            MemoryRegion.Unused => 0,
            _ => (uint)((buffer[offset + 3] << 24) | (buffer[offset + 2] << 16) | (buffer[offset + 1] << 8) |
                        buffer[offset])
        };

        return BitOperations.RotateRight(raw, (int)((address & 3u) * 8));
    }

    public ushort Read16(uint address)
    {
        address &= ~1u;
        var region = ResolveRegion(address, out var buffer, out var offset);
        return region switch
        {
            MemoryRegion.Io => memory.Io.ReadIo16Aligned(address),
            _ => (ushort)((buffer[offset + 1] << 8) | buffer[offset])
        };
    }

    public byte Read8(uint address)
    {
        var region = ResolveRegion(address, out var buffer, out var offset);
        return region switch
        {
            MemoryRegion.Io => memory.Io.ReadIo8(address),
            _ => buffer[offset]
        };
    }

    public void Write32(uint address, uint value)
    {
        address &= ~3u;
        var region = ResolveRegion(address, out var buffer, out var offset);

        switch (region)
        {
            case MemoryRegion.Bios or MemoryRegion.Rom or MemoryRegion.Unused:
                return;
            case MemoryRegion.Io:
                memory.Io.WriteIo32Aligned(address, value);
                break;
            default:
                buffer[offset + 3] = (byte)(value >> 24);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset] = (byte)value;
                break;
        }
    }

    public void Write16(uint address, ushort value)
    {
        address &= ~1u;
        var region = ResolveRegion(address, out var buffer, out var offset);
        switch (region)
        {
            case MemoryRegion.Bios or MemoryRegion.Rom or MemoryRegion.Unused:
                return;
            case MemoryRegion.Io:
                memory.Io.WriteIo16Aligned(address, value);
                break;
            default:
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset] = (byte)value;
                break;
        }
    }

    public void Write8(uint address, byte value)
    {
        var region = ResolveRegion(address, out var buffer, out var offset);
        switch (region)
        {
            case MemoryRegion.Bios or MemoryRegion.Rom or MemoryRegion.Unused:
                return;
            case MemoryRegion.Io:
                memory.Io.WriteIo8(address, value);
                break;
            default:
                buffer[offset] = value;
                break;
        }
    }

    private MemoryRegion ResolveRegion(uint address, out byte[] buffer, out int offset)
    {
        switch (address >> 24)
        {
            case 0x00:
                buffer = memory.Bios;
                offset = (int)(address % (uint)memory.Bios.Length);
                return MemoryRegion.Bios;
            case 0x02:
                buffer = memory.Ewram;
                offset = (int)((address - 0x02000000) % (uint)memory.Ewram.Length);
                return MemoryRegion.Ewram;
            case 0x03:
                buffer = memory.Iwram;
                offset = (int)((address - 0x03000000) % (uint)memory.Iwram.Length);
                return MemoryRegion.Iwram;
            case 0x04:
                buffer = [];
                offset = 0;
                return MemoryRegion.Io;
            case 0x05:
                buffer = memory.PaletteRam;
                offset = (int)((address - 0x05000000) % (uint)memory.PaletteRam.Length);
                return MemoryRegion.PaletteRam;
            case 0x06:
                buffer = memory.Vram;
                offset = (int)((address - 0x06000000) % (uint)memory.Vram.Length);
                return MemoryRegion.Vram;
            case 0x07:
                buffer = memory.Oam;
                offset = (int)((address - 0x07000000) % (uint)memory.Oam.Length);
                return MemoryRegion.Oam;
            case 0x08:
            case 0x09:
            case 0x0A:
            case 0x0B:
            case 0x0C:
            case 0x0D:
                buffer = memory.Rom;
                offset = memory.Rom.Length == 0 ? 0 : (int)((address - 0x08000000) % (uint)memory.Rom.Length);
                return MemoryRegion.Rom;
            case 0x0E:
                buffer = memory.Sram;
                offset = (int)((address - 0x0E000000) % (uint)memory.Sram.Length);
                return MemoryRegion.Sram;
            default:
                buffer = [];
                offset = 0;
                Console.WriteLine($"Address Accessed: 0x{address:x8}");
                return MemoryRegion.Unused;
        }
    }

    public int GetCpuAccessCycles(uint address, AccessWidth width, bool sequential)
    {
        return (int)(address >> 24) switch
        {
            0x00 => 1, //BIOS
            0x02 => width == AccessWidth.Word ? 6 : 3, //EWRAM
            0x03 => 1, //IWRAM
            0x04 => 1, //IO
            0x05 => width == AccessWidth.Word ? 2 : 1, //Palette RAM
            0x06 => width == AccessWidth.Word ? 2 : 1, //VRAM
            0x07 => 1, //OAM
            0x08 or 0x09 => GetGamePakRomCycles(waitState: 0, width, sequential),
            0x0A or 0x0B => GetGamePakRomCycles(waitState: 1, width, sequential),
            0x0C or 0x0D => GetGamePakRomCycles(waitState: 2, width, sequential),
            0x0E or 0x0F => 1, //SRAM
            _ => 1
        };
    }

    public int GetGamePakRomCycles(int waitState, AccessWidth width, bool sequential)
    {
        int first;
        int second;
        switch (waitState)
        {
            case 0:
                first = DecodeFirstAccess((memory.Io.REG_WAITCNT >> 2) & 0b11);
                second = ((memory.Io.REG_WAITCNT >> 4) & 1) == 0 ? 2 : 1;
                break;
            case 1:
                first = DecodeFirstAccess((memory.Io.REG_WAITCNT >> 5) & 0b11);
                second = ((memory.Io.REG_WAITCNT >> 7) & 1) == 0 ? 4 : 1;
                break;
            case 2:
                first = DecodeFirstAccess((memory.Io.REG_WAITCNT >> 8) & 0b11);
                second = ((memory.Io.REG_WAITCNT >> 10) & 1) == 0 ? 8 : 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(waitState));
        }

        int initial = sequential ? second : first;
        return width switch
        {
            AccessWidth.Byte => initial,
            AccessWidth.Halfword => initial,
            AccessWidth.Word => initial + second,
            _ => throw new ArgumentOutOfRangeException(nameof(width))
        };
    }

    private static int DecodeFirstAccess(int value)
    {
        return value switch
        {
            0 => 4,
            1 => 3,
            2 => 2,
            3 => 8,
            _ => throw new ArgumentOutOfRangeException(nameof(value)),
        };
    }
}