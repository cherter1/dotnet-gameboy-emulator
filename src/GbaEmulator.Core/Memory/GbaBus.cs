using System.Numerics;
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
}